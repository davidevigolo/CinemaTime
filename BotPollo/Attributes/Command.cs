using Discord;
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
        public string[] aliases;
        public Command(string Name, params string[] alias)
        {
            this.Name = Name;
            this.aliases = alias;
        }

    }

    class Option : Attribute
    {
        public string Name { get; set; }
        public ApplicationCommandOptionType Type { get; set; }
        public bool Required { get; set; }

        public Option(string Name, ApplicationCommandOptionType Type, bool required = false)
        {
            this.Name = Name;
            this.Type = Type;
            Required = required;
        }
    }
}
