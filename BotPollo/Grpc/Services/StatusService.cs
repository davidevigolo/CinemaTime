using BotPollo.Attributes;
using BotPollo.Core;
using BotPollo.Logging;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Configuration;

namespace BotPollo.Grpc.Services
{
    public class StatusService : BotPollo.Grpc.Status.StatusBase
    {
        public StatusService()
        {

        }

        public override async Task<StatusResponse> GetStatus(StatusRequest request, ServerCallContext context)
        {
            ulong guildId = request.GuildId;
            var player = Globals.serverPlayersMap[guildId];
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

        public override async Task<CurrentSongResponse> GetCurrentSong(CurrentSongRequest request, ServerCallContext context)
        {
            ulong guildId = request.GuildId;
            IDiscordPlayer player = Globals.serverPlayersMap[guildId];
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
        public override async Task<UserGuildPlayerResponse> GetUserGuildPlayer(UserGuildPlayerRequest request, ServerCallContext context)
        {
            ulong user_id = request.UserId;
            ulong[] players = Globals.serverPlayersMap.Where(z =>
            {
                var guildId = z.Value.AudioChannel.GuildId;
                if (!Globals.serverPlayersMap.ContainsKey(guildId)) return false;
                var audioChannel = z.Value.AudioChannel;
                if (audioChannel == null || z.Value.AudioClient.ConnectionState != Discord.ConnectionState.Connected) return false;
                return true;

            }).Select(z => z.Value.AudioChannel.GuildId).ToArray();

            var result = new UserGuildPlayerResponse();
            result.GuildIds.Add(players);
            return result;

        }
    }
}