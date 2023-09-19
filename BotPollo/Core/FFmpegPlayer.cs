using BotPollo.Logging;
using System.Diagnostics;
using System.Numerics;
using YoutubeExplode.Videos.Streams;
using Exception = System.Exception;
using TimeSpan = System.TimeSpan;

namespace BotPollo.Core
{
    public class FFmpegPlayer : IDisposable
    {
        public delegate void PlaybackEndedCallback();
        public delegate void PlaybackStartedCallback();
        public event PlaybackEndedCallback PlaybackEnded;
        public event PlaybackStartedCallback PlaybackStarted;
        protected System.Threading.CancellationTokenSource tokenSource;
        protected System.Threading.CancellationToken skipToken;
        private bool isPaused;
        internal Stream CurrentYoutubeSourceStream { get; private set; } //Original stream fetched from youtube
        internal Stream CurrentStream { get; private set; } //Original stream fetched from youtube processed by ffmpeg but not consumed by ffmpeg
        internal Stream CurrentStreamPlaying { get; private set; }  //Current stream represents the current song stream but it's just a copy, that is consumed by ffmpeg.
        internal TaskCompletionSource PauseTask { get; private set; }
        internal int StreamBitrate { get; private set; }
        internal Stopwatch songTimer { get; private set; }
        protected Task CurrentOperation { get; set; }
        protected Task CurrentSetOperation { get; set; } //Used by DiscordPlayer.cs to wait until the first song enqueued of a playlist starts playing, avoiding creating multiple message being created
        protected FFmpegPlayer()
        {
            tokenSource = new System.Threading.CancellationTokenSource();
            skipToken = tokenSource.Token;
            songTimer = new Stopwatch();
            isPaused = false;
            PauseTask = new TaskCompletionSource();
        }

        //private static async Task<Process> CreateStreamAsync(string path)
        //{
        //    return Process.Start(new ProcessStartInfo
        //    {
        //        FileName = @"C:\Users\david\Documents\ffmpeg\bin\ffmpeg.exe",
        //        Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
        //        UseShellExecute = false,
        //        RedirectStandardOutput = true,
        //    });
        //}

        ///<summary>
        /// Queues a song given a path
        /// Do not await or will wait until the song is finished
        /// </summary>
        //protected async Task SetSongStreamAsync(string path,Stream destinationStream)
        //{
        //    using (var ffmpeg = await CreateStreamAsync(path))
        //    using (var output = ffmpeg.StandardOutput.BaseStream)
        //    {
        //        PlaybackStarted();
        //        try {
        //            await output.CopyToAsync(destinationStream);
        //        }
        //        finally
        //        { await output.FlushAsync(); PlaybackEnded(); Logging.Logger.Console_Log($"Track termnated: {path.Split('\\').Last()}",Logging.LogLevel.Info); }
        //    }
        //}

        protected async Task SetYoutubeStreamAsync(Stream youtubeInputStream, Stream destinationStream, AudioOnlyStreamInfo streamInfo, int channelBitrate)
        {
            Process proc = new Process();
            MemoryStream ms = new MemoryStream();
            MemoryStream ms2 = new MemoryStream();

            int bitrateParam = channelBitrate > 256000 ? 256000 : channelBitrate;
            proc.StartInfo.FileName = @"C:\Users\david\Documents\ffmpeg\bin\ffmpeg.exe";
            proc.StartInfo.Arguments = String.Format($" -loglevel panic -i pipe:0 -ac 2 -f s16le -ar 48000 -b:a {bitrateParam} pipe:1"); //-ab {channelBitrate}
                                                                                                                                         //proc.StartInfo.Arguments = String.Format($"-i pipe:0 -c:a libopus -f opus -thread_queue_size 4096 -b:a {channelBitrate} pipe:1");
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;

            proc.Start();


            Logging.Logger.Console_Log($"Converting FFMPEG InputStream ({streamInfo.AudioCodec} | {streamInfo.Bitrate} | {streamInfo.Size}) to Discord suitable format ({channelBitrate / 1000} kb/s)", Logging.LogLevel.AudioManager);
            //StreamToStreamAsync(inputStream, proc.StandardInput.BaseStream);
            await youtubeInputStream.CopyToAsync(ms);
            CurrentYoutubeSourceStream = ms;
            StreamBitrate = channelBitrate;

            ms.Position = 0;
            Stream destStrm = proc.StandardInput.BaseStream;
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                await StreamToStreamAsync(ms, destStrm, skipToken, closeSourceStreamAfter: false);
            });
            Logging.Logger.Console_Log($"Piping stream to discord audio channel", Logging.LogLevel.AudioManager);
            var ffmpegOut = proc.StandardOutput.BaseStream;
            await StreamToStreamAsync(ffmpegOut, ms2, skipToken, false);
            ms2.Position = 0;

            MemoryStream currentStream = new MemoryStream();
            await ms2.CopyToAsync(currentStream);
            ms2.Position = 0;
            currentStream.Position = 0;
            CurrentStream = currentStream;

            CurrentStreamPlaying = ms2;

            PlaybackStarted();
            int bufferSize = 1500;
            byte[] bytes = new byte[bufferSize];

            var currentSkipToken = skipToken; //Altrimenti il metodo skip setta il nuovo cancellation token e il while può non venire a conoscienza che quello vecchio è stato cancellato poichè stava ancora inviando un buffer e quando checka di nuovo la reference è quella di quella nuova
            while (ms2.Position < ms2.Length)
            {
                try
                {
                    currentSkipToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    Logger.Console_Log("Track has been skipped or seeked", LogLevel.Info);
                    PlaybackEnded();
                    return;
                }
                if(isPaused)
                {
                    await PauseTask.Task;
                }
                int bytesRead = 0;
                bytesRead = await ms2.ReadAsync(bytes, 0, bytes.Length);
                if (bytesRead == 0) { break; }
                await destinationStream.WriteAsync(bytes, 0, bytesRead);
                Array.Clear(bytes);
            }
            await destinationStream.FlushAsync();

            /*try
            {
                await FFMpegCore.FFMpegArguments.FromPipeInput(new StreamPipeSource(ms)).OutputToPipe(new StreamPipeSink(destinationStream), options =>
                    options.WithAudioCodec(AudioCodec.Aac).WithAudioBitrate(channelBitrate)
                ).ProcessAsynchronously();
            }catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }*/

            PlaybackEnded();
            Logging.Logger.Console_Log($"Track terminated", Logging.LogLevel.Info);
            return;
        }
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

        ///<summary>
        /// Given a parameter <paramref name="originalStream"/> it's piped in ffmpeg and trimmed at the specified offset
        /// and then piped to another stream <paramref name="destinationStream"/>
        /// Do not await or will wait until the song is finished
        /// </summary>
        protected async Task SeekStreamAsync(TimeSpan offset, Stream originalStream, Stream destinationStream, int bitrate)
        {
            //CurrentStream = originalStream; //Otherwise we would lose the original stream reference, so any other seek operation would begin from whereItStopped not from the actual beginning of the song
            StreamBitrate = bitrate;
            Process proc = new Process();

            proc.StartInfo.FileName = @"C:\Users\david\Documents\ffmpeg\bin\ffmpeg.exe";
            proc.StartInfo.Arguments = String.Format($"-ss {offset.TotalSeconds} -i pipe:0 -ac 2 -f s16le -ar 48000 -b:a {bitrate} pipe:1");
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;

            proc.Start();

            Task.Run(async () => { await StreamToStreamAsync(originalStream, proc.StandardInput.BaseStream, skipToken,true,false); });
            MemoryStream tempStream = new MemoryStream();
            await StreamToStreamAsync(proc.StandardOutput.BaseStream, tempStream,skipToken,false);
            tempStream.Position = 0;
            CurrentStreamPlaying = tempStream;


            PlaybackStarted();


            int bufferSize = 1500;
            byte[] bytes = new byte[bufferSize];
            var currentSkipToken = skipToken;
            while (tempStream.Position < tempStream.Length)
            {
                try
                {
                    currentSkipToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    PlaybackEnded();
                    return;
                }
                if (isPaused)
                {
                    await PauseTask.Task;
                }
                int bytesRead = 0;
                bytesRead = await tempStream.ReadAsync(bytes, 0, bytes.Length);
                if (bytesRead == 0) { break; }
                await destinationStream.WriteAsync(bytes, 0, bytesRead);
                Array.Clear(bytes);
            }
            await destinationStream.FlushAsync();


            PlaybackEnded();
            Logging.Logger.Console_Log($"Track terminated", Logging.LogLevel.Info);
        }
        protected async Task SetStreamKeyAsync(string factor, TimeSpan currentTime, Stream originalStream, Stream destinationStream, int bitrate)
        {
            StreamBitrate = bitrate;
            Process proc = new Process();

            proc.StartInfo.FileName = @"C:\Users\david\Documents\ffmpeg\bin\ffmpeg.exe";
            proc.StartInfo.Arguments = String.Format($" -ss {Math.Floor(currentTime.TotalSeconds)} -i pipe:0 -af asetrate=48000*{factor},aresample=48000,atempo=1/{factor} -ac 2 -f s16le -ar 48000 -b:a {bitrate} pipe:1");
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;

            proc.Start();

            Task.Run(async () => { await StreamToStreamAsync(originalStream, proc.StandardInput.BaseStream, skipToken, true, false); });
            MemoryStream tempStream = new MemoryStream();
            await StreamToStreamAsync(proc.StandardOutput.BaseStream, tempStream, skipToken, false);
            tempStream.Position = 0;
            CurrentStreamPlaying = tempStream;


            PlaybackStarted();

            int bufferSize = 1500;
            byte[] bytes = new byte[bufferSize];
            var currentSkipToken = skipToken;
            while (tempStream.Position < tempStream.Length)
            {
                try
                {
                    currentSkipToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    PlaybackEnded();
                    return;
                }
                if (isPaused)
                {
                    await PauseTask.Task;
                }
                int bytesRead = 0;
                bytesRead = await tempStream.ReadAsync(bytes, 0, bytes.Length);
                if (bytesRead == 0) { break; }
                await destinationStream.WriteAsync(bytes, 0, bytesRead);
                Array.Clear(bytes);
            }
            await destinationStream.FlushAsync();

            PlaybackEnded();
        }
        protected async Task SetStreamSpeedAsync(double factor, Stream originalStream, Stream destinationStream, int bitrate)
        {
            //CurrentStream = originalStream; //Otherwise we would lose the original stream reference, so any other seek operation would begin from whereItStopped not from the actual beginning of the song
            StreamBitrate = bitrate;
            Process proc = new Process();

            proc.StartInfo.FileName = @"C:\Users\david\Documents\ffmpeg\bin\ffmpeg.exe";
            proc.StartInfo.Arguments = String.Format($"-hide_banner -loglevel panic -i pipe:0 -ac 2 -f s16le -af \"atempo={factor.ToString().Replace(',','.')}\" -ar 48000 -b:a {bitrate} pipe:1");
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;

            proc.Start();

            Task.Run(async () =>
            {
                await StreamToStreamAsync(originalStream, proc.StandardInput.BaseStream, skipToken,true,false);
            });

            PlaybackStarted();
            var ffmpegOut = proc.StandardOutput.BaseStream;
            destinationStream.Flush();
            await StreamToStreamAsync(ffmpegOut, destinationStream, skipToken, false);
            PlaybackEnded();
            Logging.Logger.Console_Log($"Track terminated", Logging.LogLevel.Info);
        }

        protected async Task SetStreamEqAsync(string eqParamsString,TimeSpan currentTime, Stream originalStream, Stream destinationStream, int bitrate) //Implementare sincronizzazione
        {
            //CurrentStream = originalStream; //Otherwise we would lose the original stream reference, so any other seek operation would begin from whereItStopped not from the actual beginning of the song
            StreamBitrate = bitrate;
            Process proc = new Process();

            proc.StartInfo.FileName = @"C:\Users\david\Documents\ffmpeg\bin\ffmpeg.exe";
            proc.StartInfo.Arguments = String.Format($"-hide_banner -loglevel panic -ss {Math.Floor(currentTime.TotalSeconds)} -i pipe:0 -ac 2 -f s16le {eqParamsString} -ar 48000 -b:a {bitrate} pipe:1");
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;

            proc.Start();

            Task.Run(async () => { await StreamToStreamAsync(originalStream, proc.StandardInput.BaseStream, skipToken, true, false); });
            MemoryStream tempStream = new MemoryStream();
            await StreamToStreamAsync(proc.StandardOutput.BaseStream, tempStream, skipToken, false);
            tempStream.Position = 0;
            CurrentStreamPlaying = tempStream;


            PlaybackStarted();

            int bufferSize = 1500;
            byte[] bytes = new byte[bufferSize];
            var currentSkipToken = skipToken;
            while (tempStream.Position < tempStream.Length)
            {
                try
                {
                    currentSkipToken.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    PlaybackEnded();
                    return;
                }
                if (isPaused)
                {
                    await PauseTask.Task;
                }
                int bytesRead = 0;
                bytesRead = await tempStream.ReadAsync(bytes, 0, bytes.Length);
                if (bytesRead == 0) { break; }
                await destinationStream.WriteAsync(bytes, 0, bytesRead);
                Array.Clear(bytes);
            }
            await destinationStream.FlushAsync();

            PlaybackEnded();

        }

        ///<summary>
        /// Joins two streams.
        /// Do not await or will wait until the song is finished
        /// </summary>
        protected async Task StreamToStreamAsync(Stream sourceStream, Stream destinationStream, System.Threading.CancellationToken cancellationToken, bool closeDestinationStreamAfter = true, bool closeSourceStreamAfter = true)
        {
            try
            {
                await sourceStream.CopyToAsync(destinationStream,4096,cancellationToken);
            }catch (Exception ex)
            {
                Logging.Logger.Console_Log(ex.Message, Logging.LogLevel.Error);
            }
            finally
            { await sourceStream.FlushAsync(); await destinationStream.FlushAsync(); if (closeSourceStreamAfter) sourceStream.Close(); if(closeDestinationStreamAfter) destinationStream.Close(); }
        }

        public virtual void Dispose()
        {
            CurrentStream.Close();
            CurrentStreamPlaying.Close();
            CurrentYoutubeSourceStream.Close();

            CurrentStream.Dispose();
            CurrentStreamPlaying.Dispose();
            CurrentYoutubeSourceStream.Dispose();

            songTimer.Stop();
        }

        public virtual void Pause()
        {
            PauseTask = new TaskCompletionSource(); //Set this first to avoid deadlocks: loop stops but waits on the old task forever (it shouldn't because a completed task skips the await)
            isPaused = true;
        }
        public virtual void Resume()
        {
            isPaused = false; //Execute this first to avoid a deadlock: Task gets returned but isPaused stays true, possibly awaiting the old task forever (it shouldn't because a completed task skips the await)
            PauseTask.SetResult();
        }
    }
}

