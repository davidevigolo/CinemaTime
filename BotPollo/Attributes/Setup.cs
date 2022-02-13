using BotPollo.Logging;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace BotPollo.Attributes
{
    class Setup
    {
        private static Dictionary<string, MethodInfo> CommandMap = new Dictionary<string, MethodInfo>();
        public static void RegisterCommands(Object obj)
        {
            var types = from t in obj.GetType().GetMethods() where t.GetCustomAttributes<Command>().Count() > 0 select t;

            foreach (MethodInfo mi in types)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Logger.Console_Log("Command: " + ((Command)mi.GetCustomAttribute(typeof(Command))).Name + " binded to method: " + mi.Name,LogLevel.Trace);
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
                Logger.Console_Log("User: " + msg.Author.Username + " Used command: " + ((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower(),LogLevel.Info);
                Console.ForegroundColor = ConsoleColor.White;
            }

            return Task.CompletedTask;
        }
    }
}
