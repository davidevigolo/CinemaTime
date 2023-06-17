using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Core.Exceptions
{
    class DiscordPlayerNotConnectedException : Exception
    {
        public DiscordPlayerNotConnectedException() : base("No valid DiscordPlayer found for this server") { }
    }
}
