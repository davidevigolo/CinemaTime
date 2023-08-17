using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UDPStatusServer.Messages
{
    internal class Message
    {
        public int OpCode { get; set; }
        public string Params { get; set; }
        public ulong GuildId { get; set; }
        public ulong ChannelId { get; set; }
    }

    internal struct GetPlayerInfoParams
    {
        public ulong GuildId { get; set; }
    }
}
