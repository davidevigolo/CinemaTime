using BotPollo.Core;
using BotPollo.Grpc;
using Discord;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using SpotifyAPI.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Attributes
{
    internal static class Globals
    {
        internal static Dictionary<ulong, IDiscordPlayer> serverPlayersMap = new Dictionary<ulong, IDiscordPlayer>();
        internal static IHost app { get; set; }
        internal static SpotifyClient spotifyClient;

        public async static Task<ulong[]> GetUserActivePlayer(ulong userId)
        {
            ulong[] players = await Globals.serverPlayersMap.ToAsyncEnumerable().WhereAwait(async z =>
            {
                var guildId = z.Key;
                var audioChannel = z.Value.AudioChannel;
                if (audioChannel == null || z.Value.AudioClient.ConnectionState != Discord.ConnectionState.Connected) return false;
                if ((await z.Value.AudioChannel.GetUserAsync(userId)) == null) return false;
                return true;

            }).Select(z => z.Value.AudioChannel.GuildId).ToArrayAsync();

            return players;
        }

        public async static Task<UserGuildPlayerResponse> GetUserActivePlayer(UserGuildPlayerRequest req, ServerCallContext serverCallContext)
        {
            ulong[] players = await Globals.serverPlayersMap.ToAsyncEnumerable().WhereAwait(async z =>
            {
                var guildId = z.Key;
                var audioChannel = z.Value.AudioChannel;
                if (audioChannel == null || z.Value.AudioClient.ConnectionState != Discord.ConnectionState.Connected) return false;
                if ((await z.Value.AudioChannel.GetUserAsync(req.UserId)) == null) return false;
                return true;

            }).Select(z => z.Value.AudioChannel.GuildId).ToArrayAsync();

            var result = new UserGuildPlayerResponse();
            result.GuildIds.Add(players);
            return result;
        }
    }
}
