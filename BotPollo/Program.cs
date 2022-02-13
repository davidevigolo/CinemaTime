using BotPollo.Core;
using BotPollo.Logging;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using MongoWrapper.MongoCore;
using System.Configuration;
using System.IO;

namespace BotPollo
{
    class Program
    {
        static void Main(string[] args) => new Program().MainAsync(args).GetAwaiter().GetResult(); //Creating never-ending command-waiting process

        public static DiscordSocketClient DiscordClient { get; private set; }
        public static string Token { get; private set; }
        public static DiscordSocketClient GetBot() { return DiscordClient; }
        public static MongoNode Node { get; private set; }
        private delegate Task ConsoleCommandAsyncCallback(string content, params string[] args);
        public async Task MainAsync(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    Logger.Console_Log($"Launch args: {args[0]} ", LogLevel.Info);
                }
                //MongoDB

                string dbName = "cinematime";
                Node = new MongoNode("mongodb://localhost", dbName);
                Node.Log += (string message) => { Logger.Console_Log(message, LogLevel.Database); };
                Node.Connect();

                //Discord
                var client = new DiscordSocketClient();

                client.Log += Logging.Logger.Client_LogAsync;
                client.MessageReceived += Attributes.Setup.Command_HandlerAsync;

                Attributes.Setup.RegisterCommands(new Commands());

                Token = File.ReadAllText("token.txt"); //It's already expired of course
                await client.LoginAsync(Discord.TokenType.Bot, Token);
                await client.StartAsync();
                DiscordClient = client;

                //Disconnecting bot from voice channels to prevent errors with DiscordPlayer

                if (args.Length > 0 && args[0].Equals("-i"))
                {
                    Logger.Console_Log("Inputs from console are now disabled", LogLevel.Warning);
                    await Task.Delay(-1);
                }
                else
                {
                    await WaitForCommandAsync(DispatchCommandAsync); //This will be awaited forever
                }
            }catch(Discord.Net.HttpException)
            {
                Logger.Console_Log("Invalid token, please check the token file",Logging.LogLevel.Fatal);
            }
        }

        private async Task WaitForCommandAsync(ConsoleCommandAsyncCallback callback)
        {
            while (true)
            {
                var cmdLine = Console.ReadLine();
                cmdLine = cmdLine.Trim(); //from ' cmd arg1 arg2' to 'cmd arg1 arg2' without spaces
                string cmdName = "";
                string[] cmdArgs = new string[0];
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
                await callback(cmdName, cmdArgs); //this prevents parallel command execution
            }
        }

        private async Task DispatchCommandAsync(string name, params string[] args)
        {
            if(name == "stop")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Logger.Console_Log("Shutting down bot...",LogLevel.Info);
                Console.ForegroundColor = ConsoleColor.White;
                await DiscordClient.StopAsync();
                Environment.Exit(0);

            }

            if(name == "disconnect" && DiscordClient.ConnectionState != Discord.ConnectionState.Disconnected)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Logger.Console_Log("Disconnecting bot...",LogLevel.Warning);
                Console.ForegroundColor = ConsoleColor.White;
                await DiscordClient.StopAsync();
                Logger.Console_Log("Bot is now OFFLINE, type connect to turn it back ONLINE", LogLevel.Warning);
            }
            else if (name == "disconnect")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Logger.Console_Log("Bot is already OFFLINE", LogLevel.Error);
                Console.ForegroundColor = ConsoleColor.White;
            }

            if(name == "reconnect" && DiscordClient.ConnectionState != Discord.ConnectionState.Connected)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Logger.Console_Log("Reconnecting bot...", LogLevel.Info);
                Console.ForegroundColor = ConsoleColor.White;
                await DiscordClient.LoginAsync(Discord.TokenType.Bot,Token);
                await DiscordClient.StartAsync();

            }
            else if(name == "reconnect")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Logger.Console_Log("Bot is already ONLINE", LogLevel.Error);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
        



    }
}
