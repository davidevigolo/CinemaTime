using BotPollo.Core;
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
    }
}
