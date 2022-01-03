using BotPollo.Core;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace BotPollo
{
    class Program
    {
        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        private static DiscordSocketClient dclient;
        private static string token;
        private delegate Task ConsoleCommandAsyncCallback(string content, params string[] args);
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static DiscordSocketClient GetProgram() { return dclient; }

        public async Task MainAsync()
        {
            var client = new DiscordSocketClient();

            client.Log += Client_Log;
            client.MessageReceived += Attributes.Setup.Command_Handler;

            Attributes.Setup.RegisterCommands(new Commands());

            token = "ODg1MTc0NTc0NTkxOTMwNDQ4.YTjNEA.UVRrGfAn961ZmDLo506qpBg2jEA"; //It's already expired of course

            await client.LoginAsync(Discord.TokenType.Bot,token);
            await client.StartAsync();

            dclient = client;
            WaitForCommandAsync(DispatchCommand);

            await Task.Delay(-1);
        }

        private async Task WaitForCommandAsync(ConsoleCommandAsyncCallback callback)
        {
            while (true)
            {
                var cmdLine = Console.ReadLine();
                string cmdName = "";
                string[] cmdArgs = new string[1];
                if (cmdLine.Contains(' '))
                {
                    var cmdIndex = cmdLine.IndexOf(" ");
                    cmdName = cmdLine.Substring(0, cmdIndex);
                    cmdArgs = cmdLine.Substring(cmdIndex).Split(' ');
                }
                else
                {
                    cmdName = cmdLine;
                }
                callback(cmdName, cmdArgs);
            }
        }

        private async Task DispatchCommand(string name, params string[] args)
        {
            if(name == "stop")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Logging][Internal] " + DateTime.Now.ToString("HH:mm:ss") + " Info     Shutting down bot...");
                Console.ForegroundColor = ConsoleColor.White;
                await dclient.StopAsync();
                Environment.Exit(0);

            }

            if(name == "disconnect" && dclient.ConnectionState != Discord.ConnectionState.Disconnected)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Logging][Internal] " + DateTime.Now.ToString("HH:mm:ss") + " Info     Disconnecting bot...");
                Console.ForegroundColor = ConsoleColor.White;
                await dclient.StopAsync();
                Console.WriteLine("[Logging][Internal] " + DateTime.Now.ToString("HH:mm:ss") + " Info     Bot is now OFFLINE, type connect to turn it back ONLINE");
            }
            else if (name == "disconnect")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Logging][Internal] " + DateTime.Now.ToString("HH:mm:ss") + " Error     Bot is already OFFLINE!");
                Console.ForegroundColor = ConsoleColor.White;
            }

            if(name == "reconnect" && dclient.ConnectionState != Discord.ConnectionState.Connected)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("[Logging][Internal] " + DateTime.Now.ToString("HH:mm:ss") + " Info     Reconnecting bot...");
                Console.ForegroundColor = ConsoleColor.White;
                await dclient.LoginAsync(Discord.TokenType.Bot,token);
                await dclient.StartAsync();

            }
            else if(name == "reconnect")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Logging][Internal] " + DateTime.Now.ToString("HH:mm:ss") + " Error     Bot is already ONLINE!");
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
        
        private Task Client_Log(Discord.LogMessage arg)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("[Logging][Discord] ");
            Console.Write(arg + "\n");
            Console.ForegroundColor = ConsoleColor.White;
            return Task.CompletedTask;
        }


    }
}
