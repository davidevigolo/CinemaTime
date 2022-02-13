using Discord;
using Discord.Audio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Core
{
    internal class DiscordPlayer : FFmpegPlayer
    {
        public delegate void SongAddedToQueue(string name,IMessageChannel commandChannel); //Command channel serve per passarlo poi agli event handler che non ce l'hanno nello scope
        public delegate void SongNowPlaying(string name, IMessageChannel commandChannel);
        public event SongAddedToQueue SongAdded;
        public event SongNowPlaying NewSongPlaying;
        private Queue songQueue = new Queue();
        private bool isPlaying = false;
        public IAudioClient AudioClient { get; private set; }
        private AudioOutStream clientOutStream;
        private IMessageChannel guildCommandChannel;

        public DiscordPlayer(IAudioClient audioClient,IMessageChannel commandChannel)
        {
            PlaybackStarted += PlaybackStartedHandler;
            PlaybackEnded += PlaybackEndedHandler;
            AudioClient = audioClient;
            clientOutStream = AudioClient.CreatePCMStream(AudioApplication.Mixed);
            guildCommandChannel = commandChannel;
        }

        public async Task<bool> AddSongAsync(string path)
        {
            if (File.Exists(path))
            {
                if (!isPlaying)
                {
                    NewSongPlaying(path.Split('\\').Last(),guildCommandChannel);
                    SetSongStreamAsync(path, clientOutStream);
                    return true;
                }
                else
                {
                    songQueue.Enqueue(path);
                    SongAdded(path.Split('\\').Last(),guildCommandChannel);
                    return false;
                }
            }
            return false;

        }

        public async Task<bool> StreamToChannelAsync(int procId)
        {
            if (!isPlaying)
            {
                Stream procStream = Process.GetProcessById(procId).StandardOutput.BaseStream;
                StreamToStreamAsync(procStream, clientOutStream);
                return true;
            }
            return false;
        }

        private void PlaybackStartedHandler()
        {
            isPlaying = true;
        }

        private void PlaybackEndedHandler() //FIXA CHE A VOLTE NON PASSA A QUELLA SUCCESIVA
        {
            if(songQueue.Count > 0)
            {
                string path = (string)songQueue.Dequeue();
                SetSongStreamAsync(path, clientOutStream);
                Logging.Logger.Console_Log($"New song playing: {path.Split('\\').Last()}",Logging.LogLevel.Info);
                NewSongPlaying(path.Split('\\').Last(),guildCommandChannel);
                return;
            }
            isPlaying = false;
        }
    }
}
