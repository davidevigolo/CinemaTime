using ABI.System;
using BotPollo.Logging;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using TimeSpan = System.TimeSpan;

namespace BotPollo.Core
{
    public class DiscordPlayer : FFmpegPlayer
    {
        public delegate void SongAddedToQueue(string name,IMessageChannel commandChannel,DiscordPlayer sender); //Command channel serve per passarlo poi agli event handler che non ce l'hanno nello scope
        public delegate void SongNowPlaying(string name, IMessageChannel commandChannel, DiscordPlayer sender);
        public event SongAddedToQueue SongAdded;
        public event SongNowPlaying NewSongPlaying;
        public Queue songQueue { get; private set; }
        public bool isPlaying { get; private set; }
        public IAudioClient AudioClient { get; private set; }
        public IVoiceChannel AudioChannel { get; set; }
        private AudioOutStream clientOutStream;
        private IMessageChannel guildCommandChannel;
        public AudioOnlyStreamInfo currentStreamInfo;
        public VideoSearchResult currentVideoInfo { get; private set; }
        public MemoryMappedFile MMF { get; private set; }
        public const int MMF_MAX_SIZE = 4092;
        public const int MMF_VIEW_SIZE = 4092;
        private bool manualInterruption = false; //Usato per quando viene ricreata la stream che parte da un'altro punto, per non eseguire PlayBackEnded e passare alla prossima canzone

        private struct QueueObject
        {
            internal IStreamInfo StreamInfo { get; set; }
            internal string Title { get; set; }
            internal string Url { get; set; }
        }

        public DiscordPlayer(IAudioClient audioClient,IMessageChannel commandChannel, IVoiceChannel audioChannel)
        {
            PlaybackStarted += PlaybackStartedHandler;
            PlaybackEnded += PlaybackEndedHandlerAsync;
            AudioChannel = audioChannel;
            AudioClient = audioClient;
            isPlaying = false;
            songQueue = new Queue();
            clientOutStream = AudioClient.CreatePCMStream(AudioApplication.Voice,audioChannel.Bitrate > 256000 ? 256000 : audioChannel.Bitrate,1000);
            guildCommandChannel = commandChannel;

            //ulong serverId = (commandChannel as IGuildChannel).Guild.Id;
            //MMF = MemoryMappedFile.CreateNew(serverId.ToString(), MMF_MAX_SIZE);
            //MemoryMappedViewStream mmfStream = MMF.CreateViewStream(0, MMF_MAX_SIZE, MemoryMappedFileAccess.ReadWrite);
            //MappedFileSettingsModel model = new MappedFileSettingsModel
            //{
            //    isPlaying = false,
            //    serverId = serverId,
            //    songPlaying = null
            //};
            //string serializedModel = JsonConvert.SerializeObject(model, Formatting.None);
            //mmfStream.Write(Encoding.UTF8.GetBytes(serializedModel));
        }

        //public async Task<bool> AddSongAsync(string path)
        //{
        //    if (File.Exists(path))
        //    {
        //        if (!isPlaying)
        //        {
        //            NewSongPlaying(path.Split('\\').Last(),guildCommandChannel);
        //            SetSongStreamAsync(path, clientOutStream);
        //            return true;
        //        }
        //        else
        //        {
        //            songQueue.Enqueue(path);
        //            SongAdded(path.Split('\\').Last(),guildCommandChannel);
        //            return false;
        //        }
        //    }
        //    return false;

        //}

        public async Task<bool> AddYoutubeSongAsync(string query)
        {
            var youtube = new YoutubeClient();

            try
            {
                var video = await youtube.Search.GetVideosAsync(query).FirstAsync();
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(video.Id);
                var streamInfos = streamManifest.GetAudioOnlyStreams();

                Bitrate br = new Bitrate(0);
                foreach (var s in streamInfos) { if (s.Bitrate > br) br = s.Bitrate; }
                var streamInfo = streamInfos.Where(x => x.Bitrate.BitsPerSecond == br.BitsPerSecond).First();

                if (streamInfo == null || streamManifest == null) return false;
                var stream = await youtube.Videos.Streams.GetAsync(streamInfo);
                int bitrate = AudioChannel.Bitrate;
                if (isPlaying) {
                    var container = new QueueObject
                    {
                        StreamInfo = streamInfo,
                        Title = video.Title,
                        Url = video.Url
                    };
                    songQueue.Enqueue(container);
                    SongAdded($"[{video.Title}]({video.Url})", guildCommandChannel, this);
                }
                else
                {
                    await SetYoutubeStreamAsync(stream, clientOutStream, streamInfo, bitrate);
                    //SetMMFProperty<string>("songPlaying", video.Title);
                    currentStreamInfo = streamInfo;
                    currentVideoInfo = video;
                    NewSongPlaying($"[{video.Title}]({video.Url})", guildCommandChannel, this);


                }
            }catch (ArgumentException ex)
            {
                Logging.Logger.Console_Log(ex.Message,Logging.LogLevel.Error);
                return false;
            }catch (InvalidOperationException ex)
            {
                Logging.Logger.Console_Log(ex.Message, Logging.LogLevel.Error);
                return false;
            }

            return true;
        }

        public async Task<bool> StreamToChannelAsync(int procId)
        {
            if (!isPlaying)
            {
                Stream procStream = Process.GetProcessById(procId).StandardOutput.BaseStream;
                //StreamToStreamAsync(procStream, clientOutStream);
                return true;
            }
            return false;
        }

        public bool Skip()
        {
            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            skipToken = tokenSource.Token;
            Logger.Console_Log("ma dio can", LogLevel.Info);
            return true;
        }

        public async Task<bool> SeekAsync(TimeSpan timespan)
        {
            long offset = (long)((StreamBitrate / 8) * timespan.TotalSeconds);
            manualInterruption = true;

            isPlaying = false;
            Stream originalStream = new MemoryStream();
            CurrentStream.Position = 0;
            byte[] buffer = new byte[4096];
            while(CurrentStream.Position < CurrentStream.Length)
            {
                int readBytes = CurrentStream.Read(buffer, 0, buffer.Length);
                if (readBytes == 0) break;
                originalStream.Write(buffer, 0, readBytes);
                Array.Clear(buffer);
            }
            originalStream.Position = 0;

            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            skipToken = tokenSource.Token;

            SeekStreamAsync(timespan, originalStream, clientOutStream, AudioChannel.Bitrate);

            manualInterruption = false;
            return true;
        }

        public TimeSpan GetTime()
        {
            long actualPosition = CurrentStream.Position;
            long totalBytes = CurrentStream.Length;
            double timeDecimal = (double)(actualPosition * 8d) / (16d * 2d * 48000d); //2 perchè sono 2 canali audio
            return TimeSpan.FromSeconds(timeDecimal);
        }

        public async Task<bool> SetSpeedAsync(double multiplier)
        {
            manualInterruption = true;

            isPlaying = false;
            Stream originalStream = new MemoryStream();
            CurrentStream.Position = 0;
            await CurrentStream.CopyToAsync(originalStream);
            originalStream.Position = 0;

            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            skipToken = tokenSource.Token;

            SetStreamSpeedAsync(multiplier, originalStream, clientOutStream, AudioChannel.Bitrate);

            manualInterruption = false;
            return true;
        }

        private void PlaybackStartedHandler()
        {
            isPlaying = true;
            /*Task.Run(async () =>
            {
                while (true)
                {
                    Logging.Logger.Console_Log(GetTime().ToString(), Logging.LogLevel.Trace);
                    await Task.Delay(1000);
                }
            });*/
        }

        private async void PlaybackEndedHandlerAsync() //FIXA CHE A VOLTE NON PASSA A QUELLA SUCCESIVA
        {
            if(songQueue.Count > 0 && !manualInterruption)
            {
                var youtube = new YoutubeClient();
                QueueObject entry = (QueueObject)songQueue.Dequeue();
                var stream = await youtube.Videos.Streams.GetAsync(entry.StreamInfo);
                await SetYoutubeStreamAsync(stream, clientOutStream, (AudioOnlyStreamInfo)entry.StreamInfo, AudioChannel.Bitrate);
                NewSongPlaying($"[{entry.Title}]({entry.Url})", guildCommandChannel,this);
                return;
            }
            isPlaying = manualInterruption ? true : false;
        }


        //MAPPED FILE STUFF

        //public struct MappedFileSettingsModel
        //{
        //    [JsonProperty("serverId")]
        //    public ulong serverId;
        //    [JsonProperty("songPlaying")]
        //    public string songPlaying;
        //    [JsonProperty("isPlaying")]
        //    public bool isPlaying;
        //}

        //private async void SetMMFProperty<T>(string propertyName, T value)
        //{
        //    MemoryMappedViewStream mmfStream = MMF.CreateViewStream(0,MMF_MAX_SIZE,MemoryMappedFileAccess.ReadWrite);
        //    byte[] buffer = new byte[MMF_MAX_SIZE];
        //    await mmfStream.ReadAsync(buffer);
        //    string bytes = Encoding.UTF8.GetString(buffer);
        //    JObject data = JsonConvert.DeserializeObject<JObject>(bytes);
        //    data[propertyName] = value.ToString();
        //    string serializedData = JsonConvert.SerializeObject(data, Formatting.None);
        //    mmfStream.Seek(0, SeekOrigin.Begin);
        //    mmfStream.Write(Encoding.UTF8.GetBytes(serializedData));
        //}

    }
}
