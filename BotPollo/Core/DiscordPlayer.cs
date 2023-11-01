using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using BotPollo.SignalR;
using BotPollo.SignalR.Models;
using Discord;
using Discord.Audio;
using DnsClient.Internal;
using Genius;
using Genius.Models;
using Genius.Models.Response;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using RestSharp;
using SpotifyAPI.Web;
using System.Collections;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using TimeSpan = System.TimeSpan;

namespace BotPollo.Core
{
    public interface IDiscordPlayer
    {
        IVoiceChannel AudioChannel { get; }
        IAudioClient AudioClient { get; }
        DiscordPlayer.QueueObject CurrentQueueObject { get; }
        IMessageChannel GuildCommandChannel { get; }
        bool isPlaying { get; }
        IUserMessage LyricsMessage { get; set; }
        IUserMessage PlayerMessage { get; }
        Queue SongQueue { get; }

        event DiscordPlayer.PlaylistNowPlaying NewPlaylistPlaying;
        event DiscordPlayer.SongNowPlaying NewSongPlaying;
        event DiscordPlayer.PlayerDisposed PlayerDestroyed;
        event DiscordPlayer.PlaylistAddedToQueue PlaylistAdded;
        event DiscordPlayer.SongAddedToQueue SongAdded;

        Task<bool> AddSongAsync(string query);
        Task<bool> ChangeFiltersAsync(int param);
        Task<bool> ChangePitchAsync(string factor);
        void Dispose();
        Task<string> GetLyrics();
        TimeSpan GetTime();
        bool HasChannelMessage();
        Task<bool> SeekAsync(TimeSpan timespan);
        Task<bool> SetSpeedAsync(double multiplier);
        PlayerUpdate GetPlayerStatus();
        void Pause();
        void Resume();
        bool Skip();
    }

    public class DiscordPlayer : FFmpegPlayer, IDisposable, IDiscordPlayer
    {
        public delegate void SongAddedToQueue(string name, IMessageChannel commandChannel, DiscordPlayer sender); //Command channel serve per passarlo poi agli event handler che non ce l'hanno nello scope
        public delegate void SongNowPlaying(string name, IMessageChannel commandChannel, DiscordPlayer sender);
        public delegate void PlaylistAddedToQueue(string[] names, IMessageChannel commandChannel, DiscordPlayer sender);
        public delegate void PlaylistNowPlaying(string[] names, IMessageChannel commandChannel, DiscordPlayer sender);
        public delegate void PlayerDisposed(ulong guildId);
        public event SongAddedToQueue SongAdded;
        public event SongNowPlaying NewSongPlaying;
        public event PlaylistAddedToQueue PlaylistAdded;
        public event PlaylistNowPlaying NewPlaylistPlaying;
        public event PlayerDisposed PlayerDestroyed;
        private SpotifyClient spotifyClient;
        public Queue SongQueue { get; private set; }
        public bool isPlaying { get; private set; }
        public IAudioClient AudioClient { get; private set; }
        public IVoiceChannel AudioChannel { get; private set; }
        private AudioOutStream clientOutStream;
        public IMessageChannel GuildCommandChannel { get; private set; }
        public QueueObject CurrentQueueObject { get; private set; }
        private System.Timers.Timer AFKTimer;
        public IUserMessage PlayerMessage { get; set; } //To keep UI cleaner in discord channels
        public IUserMessage LyricsMessage { get; set; } //Delete lyrics after playback ended
        private bool manualInterruption = false; //Usato per quando viene ricreata la stream che parte da un'altro punto, per non eseguire PlayBackEnded e passare alla prossima canzone
        private SemaphoreSlim QueueSemaphore;
        private ILogger<DiscordPlayer> logger;
        private IHubContext<PlayerHub> hubContext;
        public struct QueueObject
        {
            internal IStreamInfo StreamInfo { get; set; }
            internal IVideo VideoInfo { get; set; }
            internal string UserQuery { get; set; }
        }

        public DiscordPlayer(Microsoft.Extensions.Logging.ILogger<DiscordPlayer> logger, IAudioClient audioClient, IMessageChannel commandChannel, IVoiceChannel audioChannel,IHubContext<PlayerHub> hubContext, SpotifyClient spotifyClient)
        {
            PlaybackStarted += PlaybackStartedHandler;
            PlaybackEnded += PlaybackEndedHandlerAsync;
            AudioChannel = audioChannel;
            AudioClient = audioClient;
            isPlaying = false;
            SongQueue = new Queue();
            clientOutStream = AudioClient.CreatePCMStream(AudioApplication.Music, audioChannel.Bitrate > 256000 ? 256000 : audioChannel.Bitrate, 1000);
            GuildCommandChannel = commandChannel;
            AFKTimer = new System.Timers.Timer();
            AFKTimer.Interval = 30 * 1000;
            AFKTimer.Elapsed += AFKTimer_Elapsed;
            QueueSemaphore = new SemaphoreSlim(1);
            this.spotifyClient = spotifyClient;
            this.logger = logger;
            this.hubContext = hubContext;

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

        public override async void Dispose()
        {
            Skip();
            if (CurrentOperation != null)
                await CurrentOperation;
            base.Dispose();
            SongQueue.Clear();
            await AudioChannel.DisconnectAsync();
            AudioClient.Dispose();
            try
            {
                await PlayerMessage.DeleteAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning("Be sure to grant permissions for the bot on this channel (create,delete)");
            }
            try
            {
                if (LyricsMessage != null)
                {
                    await LyricsMessage.DeleteAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning("Be sure to grant permissions for the bot on this channel (create,delete)");
            }
            PlayerDestroyed(AudioChannel.GuildId);
        }

        private async void AFKTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!isPlaying)
            {
                Skip();
                if (CurrentOperation != null)
                    await CurrentOperation;
                await AudioChannel.DisconnectAsync();
                AudioClient.Dispose();
                try
                {
                    await PlayerMessage.DeleteAsync();
                }
                catch (Exception ex)
                {
                    await GuildCommandChannel.SendMessageAsync("Be sure to grant permissions for the bot on this channel (create,delete)");
                }
                try
                {
                    if (LyricsMessage != null)
                    {
                        await LyricsMessage.DeleteAsync();
                    }
                }
                catch (Exception ex)
                {
                    await GuildCommandChannel.SendMessageAsync("Be sure to grant permissions for the bot on this channel (create,delete)");
                }
                Dispose();
            }
        }
        private async Task ResetAFKTimerAsync()
        {
            /*lock (eventSynchronization)
            {
                AFKTimer.Elapsed -= AFKTimer_Elapsed;
                AFKTimer = new System.Timers.Timer();
                AFKTimer.Interval = 30 * 1000;
                AFKTimer.Elapsed += AFKTimer_Elapsed;
                AFKTimer.Enabled = false;
                AFKTimer.AutoReset = false;
            }*/
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
        private enum ServiceName
        {
            Youtube,
            Spotify,
            Soundcloud
        }
        public async Task<bool> AddSongAsync(string query)
        {
            await QueueSemaphore.WaitAsync();
            await ResetAFKTimerAsync();
            bool isPlaylist = false;
            ServiceName service = ServiceName.Youtube;

            if (query.Contains("https://open.spotify.com"))
            {
                service = ServiceName.Spotify;
                if (query.Contains("playlist"))
                {
                    isPlaylist = true;
                    var index = query.IndexOf("playlist/") + "playlist/".Length;
                    query = query.Substring(index, 22);
                }
                else
                {
                    string trackId = query.Substring(query.IndexOf("track/") + 6, 22);
                    FullTrack track = await spotifyClient.Tracks.Get(trackId);
                    query = "";
                    foreach (SimpleArtist artist in track.Artists)
                    {
                        query += artist.Name;
                        query += ",";
                    }
                    query += " ";
                    query += track.Name;
                }
            }
            if (query.Contains("youtube.com"))
            {
                service = ServiceName.Youtube;
                if (query.Contains("list="))
                {
                    isPlaylist = true;
                    query = query.Substring(query.IndexOf("list=") + "list=".Length);
                }
            }

            var youtube = new YoutubeClient();

            try
            {
                switch (service)
                {
                    case ServiceName.Youtube:
                        if (isPlaylist)
                        {
                            var playlist = await youtube.Playlists.GetAsync(query);
                            var videos = await youtube.Playlists.GetVideosAsync(playlist.Id);
                            List<string> titles = new List<string>();
                            for (int i = 0; i < videos.ToArray().Length; i++)
                            {
                                titles.Add(videos[i].Title);
                            }
                            await AddSongsToQueue(videos.ToArray(), titles.ToArray(), youtube);
                        }
                        else
                        {
                            var video = await youtube.Search.GetVideosAsync(query).FirstAsync();
                            await AddSongToQueue(video, query, youtube);
                        }
                        break;
                    case ServiceName.Spotify:
                        if (isPlaylist)
                        {
                            var playlist = await spotifyClient.Playlists.Get(query);
                            List<IVideo> videos = new List<IVideo>();
                            List<string> queries = new List<string>();
                            for (int i = 0; i < playlist.Tracks.Total; i++)
                            {
                                var track = playlist.Tracks.Items[i];
                                if (track.Track is FullTrack fullTrack)
                                {
                                    string tempQuery = "";
                                    foreach (var artist in fullTrack.Artists)
                                    {
                                        tempQuery += artist.Name;
                                        tempQuery += ",";
                                    }
                                    tempQuery += " ";
                                    tempQuery += fullTrack.Name;
                                    var video = await youtube.Search.GetVideosAsync(fullTrack.Name).FirstAsync();
                                    videos.Add(video);
                                    await Task.Delay(500);
                                    queries.Add(tempQuery);
                                }
                            }
                            await AddSongsToQueue(videos.ToArray(), queries.ToArray(), youtube);
                        }
                        else
                        {
                            var video = await youtube.Search.GetVideosAsync(query).FirstAsync();
                            await AddSongToQueue(video, query, youtube);
                        }
                        break;
                }



            }
            catch (ArgumentException ex)
            {
                logger.LogError(ex.Message);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex.Message);
                return false;
            }
            finally
            {
                QueueSemaphore.Release();
            }
            return true;
        }
        private async Task<AudioOnlyStreamInfo?> GetStreamInfo(VideoId id, YoutubeClient youtubeClient)
        {
            var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(id);
            var streamInfos = streamManifest.GetAudioOnlyStreams();

            Bitrate br = new Bitrate(0);
            foreach (var s in streamInfos) { if (s.Bitrate > br) br = s.Bitrate; }
            var streamInfo = streamInfos.Where(x => x.Bitrate.BitsPerSecond == br.BitsPerSecond).First();

            if (streamInfo == null || streamManifest == null) return null;
            return streamInfo;
        }
        private async Task<int> AddSong(IVideo video, string query, YoutubeClient client)
        {
            var task = hubContext.Clients.Group(AudioChannel.GuildId.ToString()).SendAsync("ReceiveMessage", $"Added song {video.Title}");
            var streamInfo = await GetStreamInfo(video.Id, client);
            var stream = await client.Videos.Streams.GetAsync(streamInfo);
            int bitrate = AudioChannel.Bitrate;

            if (isPlaying)
            {
                var container = new QueueObject
                {
                    StreamInfo = streamInfo,
                    VideoInfo = video,
                    UserQuery = query
                };
                SongQueue.Enqueue(container);
                await hubContext.Clients.Group(AudioChannel.GuildId.ToString()).SendAsync("QueueUpdate", SongQueue.ToArray());
                await task;
                return 1;
                //SongAdded($"[{video.Title}]({video.Url})", guildCommandChannel, this);
            }
            else
            {
                CurrentQueueObject = new QueueObject //this goes first because if for any reason SetYoutubeStream starts the playback before this object is set, NullPointer will be thrown as the message will have null video info.
                {
                    StreamInfo = streamInfo,
                    VideoInfo = video,
                    UserQuery = query
                };
                CurrentOperation = SetYoutubeStreamAsync(stream, clientOutStream, streamInfo, bitrate);
                isPlaying = true; //This is set to true previously to avoid multiple NewSongPlaying events being fired on multiple songs
                await hubContext.Clients.Group(AudioChannel.GuildId.ToString()).SendAsync("QueueUpdate", SongQueue.ToArray());
                await task;
                return 0;
                //NewSongPlaying($"[{video.Title}]({video.Url})", guildCommandChannel, this);
            }
        }
        private async Task AddSongsToQueue(IVideo[] videos, string[] queries, YoutubeClient client)
        {
            for (int i = 0; i < videos.Length; i++)
            {
                await AddSong(videos[i], queries[i], client);
            }
            List<string> names = new List<string>();
            for (int i = 0; i < videos.Length; i++)
            {
                names.Add(videos[i].Title);
            }
            PlaylistAdded(names.ToArray(), GuildCommandChannel, this);
        }
        private async Task AddSongToQueue(IVideo video, string query, YoutubeClient client)
        {
            int eventNumber = await AddSong(video, query, client);
            if (eventNumber == 0)
                NewSongPlaying($"[{video.Title}]({video.Url})", GuildCommandChannel, this);
            else
                SongAdded($"[{video.Title}]({video.Url})", GuildCommandChannel, this);
        }

        public bool Skip()
        {
            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            skipToken = tokenSource.Token;
            return true;
        }

        public async Task<bool> SeekAsync(TimeSpan timespan)
        {
            await ResetAFKTimerAsync();
            if (!isPlaying || CurrentYoutubeSourceStream is null) return false;
            long offset = (long)((StreamBitrate / 8) * timespan.TotalSeconds);
            manualInterruption = true;

            isPlaying = false;
            Stream originalStream = new MemoryStream();
            CurrentYoutubeSourceStream.Position = 0; //This is null when seeking with no song playing
            byte[] buffer = new byte[4096];
            while (CurrentYoutubeSourceStream.Position < CurrentYoutubeSourceStream.Length)
            {
                int readBytes = CurrentYoutubeSourceStream.Read(buffer, 0, buffer.Length);
                if (readBytes == 0) break;
                originalStream.Write(buffer, 0, readBytes);
                Array.Clear(buffer);
            }
            originalStream.Position = 0;

            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            skipToken = tokenSource.Token;

            await CurrentOperation; //awaits for the previous stream to receive the token cancellation notification and stop completely. This avoids overlapping of two streams due to manualInterruption being already set to false when executing PlaybackHandlerAsync()

            CurrentOperation = SeekStreamAsync(timespan, originalStream, clientOutStream, AudioChannel.Bitrate);

            manualInterruption = false;
            return true;
        }
        public async Task<bool> ChangePitchAsync(string factor)
        {
            await ResetAFKTimerAsync();
            manualInterruption = true;
            isPlaying = false;
            Stream originalStream = new MemoryStream();
            CurrentYoutubeSourceStream.Position = 0;
            byte[] buffer = new byte[4096];
            while (CurrentYoutubeSourceStream.Position < CurrentYoutubeSourceStream.Length)
            {
                int readBytes = CurrentYoutubeSourceStream.Read(buffer, 0, buffer.Length);
                if (readBytes == 0) break;
                originalStream.Write(buffer, 0, readBytes);
                Array.Clear(buffer);
            }
            originalStream.Position = 0;

            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            skipToken = tokenSource.Token;

            await CurrentOperation; //awaits for the previous stream to receive the token cancellation notification and stop completely. This avoids overlapping of two streams due to manualInterruption being already set to false when executing PlaybackHandlerAsync()

            CurrentOperation = SetStreamKeyAsync(factor, GetTime(), originalStream, clientOutStream, AudioChannel.Bitrate);

            manualInterruption = false;
            return true;
        }
        public async Task<bool> ChangeFiltersAsync(int param)
        {
            await ResetAFKTimerAsync();
            string eqParamsString = "-af \"";
            string additionalFilters = "";
            switch (param)
            {
                case 0:
                    eqParamsString += "firequalizer=gain_entry='entry(50,4);entry(60,4);entry(70,4);entry(158,-4);entry(450,-5);entry(688,-4);entry(1250,-6);entry(2700,0);entry(5300,1);entry(7500,0);entry(12110,3)'";
                    break;
                case 1:
                    eqParamsString += "equalizer=f=70:width_type=h:width=20:g=2";
                    break;
                case 2:
                    eqParamsString += "equalizer=f=8000:width_type=h:width=2000:g=4";
                    break;
                case 3:
                    eqParamsString += "firequalizer=gain_entry='entry(115,3);entry(250,2);entry(450,-6);entry(734,-4);entry(1250,2);entry(2700,3);entry(5300,4);entry(7500,5);entry(13420,7)'";
                    break;
                case 4:
                    eqParamsString += "firequalizer=gain_entry='entry(115,3);entry(250,2);entry(450,-6);entry(734,-4);entry(1250,2);entry(2700,3);entry(5300,4);entry(7500,5);entry(13420,7)'";
                    additionalFilters = "apulsator=hz=0.125";
                    break;
                case 5:
                    eqParamsString += "firequalizer=gain_entry='entry(115,3);entry(250,2);entry(450,-6);entry(734,-4);entry(1250,2);entry(2700,3);entry(5300,4);entry(7500,5);entry(13420,7)'";
                    additionalFilters = "lowpass=f=100";
                    break;
            }
            if (additionalFilters != "")
                eqParamsString += "," + additionalFilters + "\"";
            else
                eqParamsString += "\"";
            manualInterruption = true;
            isPlaying = false;
            Stream originalStream = new MemoryStream();
            CurrentYoutubeSourceStream.Position = 0;
            byte[] buffer = new byte[4096];
            while (CurrentYoutubeSourceStream.Position < CurrentYoutubeSourceStream.Length)
            {
                int readBytes = CurrentYoutubeSourceStream.Read(buffer, 0, buffer.Length);
                if (readBytes == 0) break;
                originalStream.Write(buffer, 0, readBytes);
                Array.Clear(buffer);
            }
            originalStream.Position = 0;

            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            skipToken = tokenSource.Token;

            await CurrentOperation; //awaits for the previous stream to receive the token cancellation notification and stop completely. This avoids overlapping of two streams due to manualInterruption being already set to false when executing PlaybackHandlerAsync()

            CurrentOperation = SetStreamEqAsync(eqParamsString, GetTime(), originalStream, clientOutStream, AudioChannel.Bitrate);

            manualInterruption = false;
            return true;
        }

        public async Task<string> GetLyrics()
        {
            await ResetAFKTimerAsync();
            string apiKey = "6gcdh8TnNKtQUi2oMJzIBe56RR_x-uDzSSbTaMS86iwIlrDYIRR68wEG1EQ5XQn0";
            GeniusClient client = new GeniusClient(apiKey);
            Genius.Models.Response.SearchResponse response = await client.SearchClient.Search(CurrentQueueObject.UserQuery);
            SearchHit hit = response.Response.Hits.FirstOrDefault();
            if (hit == null)
            {
                throw new ArgumentException("Not found");
            }
            SongResponse songResponse = await client.SongClient.GetSong(hit.Result.Id);
            string url = songResponse.Response.Song.Url;

            var restClient = new RestClient();
            var restRequest = new RestRequest(url);
            restRequest.AddHeader("Authorization", $"Bearer {apiKey}");
            var restResponse = restClient.Execute<string>(restRequest);
            var lyrics = restResponse.Content;

            HtmlParser parser = new HtmlParser();
            var parsedPage = parser.ParseDocument(lyrics);
            var lyricsResult = parsedPage.QuerySelectorAll("div[data-lyrics-container]");
            string res = "";
            foreach (var lyricsItem in lyricsResult)
            {
                RemoveAllTagsExceptBr(lyricsItem);
                var temp = lyricsItem.InnerHtml;
                temp = temp.Replace("<br>", "\r\n");
                temp = temp.Replace("</br>", "\r\n");
                res += temp;
                res += "\r\n\r\n";
            }

            res = res.Replace("<br>", "\r\n");
            res = res.Replace("</br>", "\r\n");

            return res;

        }

        private static void RemoveAllTagsExceptBr(IElement node)
        {
            for (int i = node.ChildNodes.Count() - 1; i >= 0; i--)
            {
                var childNode = node.ChildNodes[i];

                if (childNode.NodeName.ToLower() != "br" && childNode.NodeName.ToLower() != "#text")
                {
                    node.RemoveChild(childNode);
                }
            }
        }

        public TimeSpan GetTime()
        {
            long delta = CurrentStream.Length - CurrentStreamPlaying.Length;
            long actualPosition = delta + CurrentStreamPlaying.Position; //IL problema sta qui, la posizione va relativa alla stream originale
            double timeDecimal = (double)(actualPosition * 8d) / (16d * 2d * 48000d); //2 perchè sono 2 canali audio
            return TimeSpan.FromSeconds(timeDecimal);
        }

        public async Task<bool> SetSpeedAsync(double multiplier)
        {
            await ResetAFKTimerAsync();
            manualInterruption = true;

            isPlaying = false;
            Stream originalStream = new MemoryStream();
            CurrentStream.Position = 0;
            await CurrentStream.CopyToAsync(originalStream);
            originalStream.Position = 0;

            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            skipToken = tokenSource.Token;

            await CurrentOperation; //awaits for the previous stream to receive the token cancellation notification and stop completely. This avoids overlapping of two streams due to manualInterruption being already set to false when executing PlaybackHandlerAsync()

            SetStreamSpeedAsync(multiplier, originalStream, clientOutStream, AudioChannel.Bitrate);

            manualInterruption = false;
            return true;
        }

        private async void PlaybackStartedHandler()
        {
            isPlaying = true;
            await ResetAFKTimerAsync();
            await PlayerUpdate(); //It is better to update the player here to have better sync with the web interface (In case of exception playing the song this won't be executed (this is the correct behaviour))
            /*Task.Run(async () =>
            {
                while (true)
                {
                    Logging.Logger.Console_Log(GetTime().ToString(), Logging.LogLevel.Trace);
                    await Task.Delay(1000);
                }
            });*/
        }

        private async void PlaybackEndedHandlerAsync()
        {
            if (!manualInterruption)
            {
                if (SongQueue.Count > 0)
                {
                    var youtube = new YoutubeClient();
                    QueueObject entry = (QueueObject)SongQueue.Dequeue();
                    var stream = await youtube.Videos.Streams.GetAsync(entry.StreamInfo);
                    isPlaying = false;
                    PlayerUpdate(); //This will update the isPlaying status on web clients but won't set the next song until it is actually played successfully (see PlaybackStarted handler)
                    CurrentOperation = SetYoutubeStreamAsync(stream, clientOutStream, (AudioOnlyStreamInfo)entry.StreamInfo, AudioChannel.Bitrate);
                    CurrentQueueObject = entry;
                    if (LyricsMessage != null)
                    {
                        await LyricsMessage.DeleteAsync();
                    }
                    return;
                }
                else
                {
                    AFKTimer.Enabled = true;
                    AFKTimer.AutoReset = false;
                    isPlaying = false;
                    PlayerUpdate();
                }
            }
        }

        public bool HasChannelMessage()
        {
            return PlayerMessage != null;
        }

        public override async void Pause()
        {
            base.Pause();
            isPlaying = false;
            await hubContext.Clients.Group(AudioChannel.GuildId.ToString()).SendAsync("PlayerUpdate", GetPlayerStatus());
        }
        public override async void Resume()
        {
            base.Resume();
            isPlaying = true;
            await hubContext.Clients.Group(AudioChannel.GuildId.ToString()).SendAsync("PlayerUpdate", GetPlayerStatus());
        }

        /*private struct PlayerStatus
        {
            public bool isPlaying;
            public TimeSpan position;
            public QueueObject currentSong;
        }*/

        public PlayerUpdate GetPlayerStatus()
        {
            return new PlayerUpdate(
                isPlaying,
                CurrentQueueObject.VideoInfo.Url,
                CurrentQueueObject.VideoInfo.Id,
                CurrentQueueObject.VideoInfo.Title,
                CurrentQueueObject.VideoInfo.Author,
                CurrentQueueObject.StreamInfo.Container,
                CurrentQueueObject.StreamInfo.Bitrate,
                CurrentQueueObject.VideoInfo.Duration,
                GetTime(),
                CurrentQueueObject.VideoInfo.Thumbnails
                );
        }

        private async Task PlayerUpdate()
        {
            var temp = GetPlayerStatus();
            await hubContext.Clients.Group(AudioChannel.GuildId.ToString()).SendAsync("PlayerUpdate", GetPlayerStatus());
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
