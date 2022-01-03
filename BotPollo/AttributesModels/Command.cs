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
        public Command(string Name)
        {
            this.Name = Name;
        }
        public string Name { get; set; }
    }

    class Setup
    {
        private static Dictionary<string, MethodInfo> CommandMap = new Dictionary<string, MethodInfo>();
        public static void RegisterCommands(Object obj)
        {
            var types = from t in obj.GetType().GetMethods() where t.GetCustomAttributes<Command>().Count() > 0 select t;

            foreach (MethodInfo mi in types)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[Logging][Internal]" + DateTime.Now.ToString(" HH:mm:ss") + " Info     Command: " + ((Command)mi.GetCustomAttribute(typeof(Command))).Name + " binded to method: " + mi.Name);
                CommandMap.Add(((Command)mi.GetCustomAttribute(typeof(Command))).Name.ToLower(), mi);
            }
        }

        internal static Task Command_Handler(SocketMessage msg)
        {
            if (CommandMap.ContainsKey(msg.Content.ToLower()))
            {
                var method = CommandMap.GetValueOrDefault(msg.Content.ToLower());
                method.Invoke(null, new object[] { msg });
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[Logging][Internal]" + DateTime.Now.ToString(" HH:mm:ss") + " Info     User: " + msg.Author.Username + " Used command: " + ((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower());
                Console.ForegroundColor = ConsoleColor.White;
            }

            return Task.CompletedTask;
        }
    }
}
