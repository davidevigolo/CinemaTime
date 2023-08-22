using BotPollo.Core;
using BotPollo.Logging;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace BotPollo.Grpc.Services
{
    public class StatusService : BotPollo.Grpc.Status.StatusBase
    {
        public StatusService()
        {

        }

        public async override Task<StatusResponse> GetStatus(StatusRequest request, ServerCallContext context)
        {
            ulong guildId = request.GuildId;
            var player = Commands.serverPlayersMap[guildId];
            if (player == null)
            {
                return null;
            }

            return new StatusResponse
            {
                IsPlaying = player.isPlaying,
                MessageChannel = player.GuildCommandChannel.Id,
                VoiceChannel = player.AudioChannel.Id
            };
        }

        public async override Task<CurrentSongResponse> GetCurrentSong(CurrentSongRequest request, ServerCallContext context)
        {
            ulong guildId = request.GuildId;
            DiscordPlayer player = Commands.serverPlayersMap[guildId];
            if (player == null)
            {
                return null;
            }

            List<string> thumbnailUrls = new List<string>();

            CurrentSongResponse result = new CurrentSongResponse
            {
                Author = player.CurrentQueueObject.VideoInfo.Author.ChannelTitle,
                Bitrate = player.CurrentQueueObject.StreamInfo.Bitrate.KiloBitsPerSecond,
                Title = player.CurrentQueueObject.VideoInfo.Title,
                Url = player.CurrentQueueObject.VideoInfo.Url
            };

            foreach (var thumbnail in player.CurrentQueueObject.VideoInfo.Thumbnails)
            {
                result.ThumbnailUrls.Add(thumbnail.Url);
            }

            return result;
        }
    }
}