using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Attributes
{
    class Command : Attribute
    {
        public string Name { get; set; }
        public Command(string Name)
        {
            this.Name = Name;
        }

    }
}
