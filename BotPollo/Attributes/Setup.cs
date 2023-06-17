using BotPollo.Logging;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Interop;

namespace BotPollo.Attributes
{
    class Setup
    {
        private static Dictionary<string, MethodInfo> CommandMap = new Dictionary<string, MethodInfo>();
        public static void RegisterCommands(Object obj)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var types = from t in obj.GetType().GetMethods() where t.GetCustomAttributes<Command>().Count() > 0 select t;

            foreach (MethodInfo mi in types)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Logger.Console_Log("Command: " + ((Command)mi.GetCustomAttribute(typeof(Command))).Name + " binded to method: " + mi.Name,LogLevel.Trace);
                var aliases = ((Command)mi.GetCustomAttribute(typeof(Command))).aliases;
                foreach(string alias in aliases)
                {
                    CommandMap.Add(alias, mi);
                    Logger.Console_Log("Alias: " + alias + " binded to method: " + mi.Name, LogLevel.Trace);
                }
                CommandMap.Add(((Command)mi.GetCustomAttribute(typeof(Command))).Name.ToLower(), mi);

                try
                {
                    var guildCommand = new SlashCommandBuilder();
                    guildCommand.WithName(((Command)mi.GetCustomAttribute(typeof(Command))).Name.ToLower())
                    .WithDescription("Listen to a song");

                    foreach(Option op in mi.GetCustomAttributes<Option>())
                    {
                        guildCommand.AddOption(op.Name, op.Type, "default description", op.Required);
                    }

                    Program.GetBot().CreateGlobalApplicationCommandAsync(guildCommand.Build());
                }
                catch (ApplicationCommandException exception)
                {
                    Logger.Console_Log("Error registering slash command", LogLevel.Error);
                }
            }

            foreach(KeyValuePair<string, MethodInfo> kvp in CommandMap)
            {
                Logger.Console_Log($"{kvp.Key} => {kvp.Value.Name}()",LogLevel.Trace);
            }

            sw.Stop();

            Logger.Console_Log($"Setup finished in {sw.ElapsedMilliseconds}ms",LogLevel.Info);
        }

        public static async Task UserJoinedVChannelHandlerAsync(SocketUser user, SocketVoiceState state, SocketVoiceState state2)
        {
            if (state.VoiceChannel == state2.VoiceChannel || !Program.EnableNotifications) return;
            //if(user.Username == "")
            /*NotifyIcon notify = new NotifyIcon();
            notify.Icon = System.Drawing.SystemIcons.Shield;
            notify.BalloonTipTitle = "Title";
            notify.BalloonTipText = "Text";
            notify.BalloonTipIcon = ToolTipIcon.Info;
            if (state.VoiceChannel == null)
            {
                Logger.Console_Log($"User {user.Username} joined {state2.VoiceChannel}", LogLevel.Info);
                notify.BalloonTipTitle = $"{user.Username}";
                notify.BalloonTipText = $"joined {state2.VoiceChannel}";

            }else if (state2.VoiceChannel == null)
            {
                Logger.Console_Log($"User {user.Username} left {state.VoiceChannel}", LogLevel.Info);
                notify.BalloonTipTitle = $"{user.Username}";
                notify.BalloonTipText = $"left {state.VoiceChannel}";
                notify.BalloonTipIcon = ToolTipIcon.Error;
            }
            else
            {
                Logger.Console_Log($"User {user.Username} moved from {state.VoiceChannel} to {state2.VoiceChannel}", LogLevel.Info);
                notify.BalloonTipTitle = $"{user.Username} moved to";
                notify.BalloonTipText = $"{state2.VoiceChannel}";
                notify.BalloonTipIcon = ToolTipIcon.Warning;
            }
            notify.Visible = true;
            notify.ShowBalloonTip(500);*/

        }

        internal static async Task Command_HandlerAsync(SocketMessage msg)
        {
            if (CommandMap.ContainsKey(msg.Content.Split(' ')[0].ToLower())) //Split serve a prendere la parte del messaggio contenente il nome del comando
            {
                var method = CommandMap.GetValueOrDefault(msg.Content.Split(' ')[0].ToLower());
                method.Invoke(null, new object[] { msg });
                Console.ForegroundColor = ConsoleColor.Cyan;
                Logger.Console_Log("User: " + msg.Author.Username + " Used command: " + ((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower(), LogLevel.Info);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        internal static async Task SlashCommandHandlerAsync(SocketSlashCommand command)
        {
            if (CommandMap.ContainsKey(command.CommandName)) //Split serve a prendere la parte del messaggio contenente il nome del comando
            {
                var method = CommandMap.GetValueOrDefault(command.CommandName);
                method.Invoke(null,new object[] { command });

                Console.ForegroundColor = ConsoleColor.Cyan;
                Logger.Console_Log("User: " + command.User.Username + " Used slash command: " + ((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower(), LogLevel.Info);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

    }
}
