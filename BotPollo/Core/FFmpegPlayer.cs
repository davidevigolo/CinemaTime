using Discord;
using Discord.Audio;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BotPollo.Core
{
    internal class FFmpegPlayer
    {
        public delegate void PlaybackEndedCallback();
        public delegate void PlaybackStartedCallback();
        public event PlaybackEndedCallback PlaybackEnded;
        public event PlaybackStartedCallback PlaybackStarted;
        private Queue songQueue = new Queue();
        private bool isPlaying = false;

        private static async Task<Process> CreateStreamAsync(string path)
        {
            return Process.Start(new ProcessStartInfo
            {
                FileName = @"C:\Program Files\FFmpeg\ffmpeg-2022-02-14-git-59c647bcf3-full_build\bin\ffmpeg.exe",
                Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            });
        }

        ///<summary>
        /// Queues a song given a path
        /// Do not await or will wait until the song is finished
        /// </summary>
        protected async Task SetSongStreamAsync(string path,Stream destinationStream)
        {
            using (var ffmpeg = await CreateStreamAsync(path))
            using (var output = ffmpeg.StandardOutput.BaseStream)
            {
                PlaybackStarted();
                try {
                    await output.CopyToAsync(destinationStream);
                }
                finally
                { await output.FlushAsync(); PlaybackEnded(); Logging.Logger.Console_Log($"Track termnated: {path.Split('\\').Last()}",Logging.LogLevel.Info); }
            }
        }

        ///<summary>
        /// Joins two streams.
        /// Do not await or will wait until the song is finished
        /// </summary>
        protected async Task StreamToStreamAsync(Stream sourceStream, Stream destinationStream)
        {
            try
            {
                await sourceStream.CopyToAsync(destinationStream);
            }
            finally
            { await sourceStream.FlushAsync(); await destinationStream.FlushAsync(); }
        }
    }
}
