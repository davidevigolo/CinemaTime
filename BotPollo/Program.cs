using BotPollo.Core;
using BotPollo.Logging;
using BotPollo.UDP;
using BotPolloG.Grpc;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SpotifyAPI.Web;
using System.Diagnostics;

namespace BotPollo
{
    class Program
    {
        //static void Main(string[] args) => new Program().MainAsync(args).GetAwaiter().GetResult(); //Creating never-ending command-waiting process

        public static DiscordSocketClient DiscordClient { get; private set; }
        public static string Token { get; private set; }
        public static DiscordSocketClient GetBot() { return DiscordClient; }
        //public static MongoNode Node { get; private set; }
        public static SpotifyClient SpotifyClient { get; private set; }
        public static bool EnableNotifications { get; set; }
        private delegate Task ConsoleCommandAsyncCallback(string content, params string[] args);
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder();
            BuildConfig(builder);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.Seq("http://localhost:5341")
                .Enrich.WithProcessId()
                .Enrich.WithThreadName()
                .Enrich.WithProcessId()
                .Enrich.WithProcessName()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithMemoryUsage()
                .CreateLogger();

            Log.Logger.Information("Application starting");

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IDiscordBotService,DiscordBotService>();
                })
                .UseSerilog()
                .Build();

            var svc = ActivatorUtilities.CreateInstance<IDiscordBotService>(host.Services);

            svc.Run();
        }
        static void BuildConfig(IConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }
        public async Task MainAsync(string[] args)
        {
            try
            {
                if (args.Length > 0)
                {
                    Log.Logger.Information($"Launch args: {args[0]}");
                }
                //MongoDB

                string dbName = "cinematime";
                /*Node = new MongoNode("mongodb://localhost", dbName);
                Node.Log += (string message) => { Logger.Console_Log(message, LogLevel.Database); };
                Node.Connect();*/
                EnableNotifications = true;

                #region grpcsetup

                GrpcSetup.GrpcStartup(new string[] { });

                #endregion
                //Discord
                DiscordSocketConfig dSocketConfig = new DiscordSocketConfig();
                dSocketConfig.GatewayIntents = Discord.GatewayIntents.MessageContent | Discord.GatewayIntents.All;
                var client = new DiscordSocketClient(dSocketConfig);

                client.Log += Logging.Logger.Client_LogAsync;
                client.MessageReceived += Attributes.Setup.Command_HandlerAsync;
                client.UserVoiceStateUpdated += Attributes.Setup.UserJoinedVChannelHandlerAsync;
                client.SlashCommandExecuted += Attributes.Setup.SlashCommandHandlerAsync;
                client.ButtonExecuted += Commands.HandleButtonInteractionAsync;
                client.Ready += async () => { Attributes.Setup.RegisterCommands(new Commands()); };

                Token = File.ReadAllText("token.txt"); //It's already expired of course
                await client.LoginAsync(Discord.TokenType.Bot, Token);
                await client.StartAsync();
                DiscordClient = client;

                //Spotify

                var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new ClientCredentialsAuthenticator("5b60c795a9874f5b943362f0020b47d4", "99516aca0c26435398e543831e0c7113"));

                SpotifyClient = new SpotifyClient(config);

                //SpotifyEnd

                Log.Logger.Information("Starting Api server...");
                Thread t = new Thread(() =>
                {
                    ConnectionManager cm = new ConnectionManager(Commands.serverPlayersMap);
                    cm.Start();  

                });
                t.Start();

                Log.Logger.Information("Api server started succesfully");

                //Disconnecting bot from voice channels to prevent errors with DiscordPlayer

                if (args.Length > 0 && args[0].Equals("-i"))
                {
                    Log.Logger.Warning("Inputs from console are now disabled!");
                    await Task.Delay(-1);
                }
                else
                {
                    await WaitForCommandAsync(DispatchCommandAsync); //This will be awaited forever
                }
            }
            catch (Discord.Net.HttpException)
            {
                Log.Logger.Fatal("Invalid Discord token.");
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
                Commands.serverPlayersMap.Clear();
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

            if(name == "togglenotify")
            {
                EnableNotifications = !EnableNotifications;
                string s = EnableNotifications ? "on" : "off";
                Logger.Console_Log($"Notifications have been turned {s}",LogLevel.Trace);
            }
            if(name == "pollo")
            {
                for(int i = 0; i < 20; i++)
                {
                    Logger.Console_Log($"PUNTIIIII",LogLevel.All);
                }
            }
            if(name == "triplipunti")
            {
                Process proc = new Process();

                proc.StartInfo.FileName = @"D:\Program Files\Rockstar Games\Grand Theft Auto V\GTAVLauncher.exe";
                proc.StartInfo.UseShellExecute = false;

                proc.Start();
            }
            if(name == "serverlist")
            {

                Logger.Console_Log("Servers active:", LogLevel.Info);
                foreach (var guild in DiscordClient.Guilds)
                {
                    Logger.Console_Log($"{guild.Name} - {guild.MemberCount} Users - {guild.PremiumTier.ToString()}", LogLevel.Info);
                }
            }
        }

        class DiscordBotService : IDiscordBotService
        {
            private readonly ILogger<DiscordBotService> _logger;
            private readonly IConfiguration _conf;
            public DiscordBotService(ILogger<DiscordBotService> logger, IConfiguration config)
            {
                _logger = logger;
                _conf = config;
            }
            public async void Run()
            {
                try
                {
                    /*if (args.Length > 0)
                    {
                        Log.Logger.Information($"Launch args: {args[0]}");
                    }*/
                    //MongoDB

                    string dbName = "cinematime";
                    /*Node = new MongoNode("mongodb://localhost", dbName);
                    Node.Log += (string message) => { Logger.Console_Log(message, LogLevel.Database); };
                    Node.Connect();*/
                    EnableNotifications = true;

                    #region grpcsetup

                    GrpcSetup.GrpcStartup(new string[] { });

                    #endregion
                    //Discord
                    DiscordSocketConfig dSocketConfig = new DiscordSocketConfig();
                    dSocketConfig.GatewayIntents = Discord.GatewayIntents.MessageContent | Discord.GatewayIntents.All;
                    var client = new DiscordSocketClient(dSocketConfig);

                    client.Log += Logging.Logger.Client_LogAsync;
                    client.MessageReceived += Attributes.Setup.Command_HandlerAsync;
                    client.UserVoiceStateUpdated += Attributes.Setup.UserJoinedVChannelHandlerAsync;
                    client.SlashCommandExecuted += Attributes.Setup.SlashCommandHandlerAsync;
                    client.ButtonExecuted += Commands.HandleButtonInteractionAsync;
                    client.Ready += async () => { Attributes.Setup.RegisterCommands<Commands>(); };

                    Token = File.ReadAllText("token.txt"); //It's already expired of course
                    await client.LoginAsync(Discord.TokenType.Bot, Token);
                    await client.StartAsync();
                    DiscordClient = client;

                    //Spotify

                    var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new ClientCredentialsAuthenticator("5b60c795a9874f5b943362f0020b47d4", "99516aca0c26435398e543831e0c7113"));

                    SpotifyClient = new SpotifyClient(config);

                    //SpotifyEnd

                    Log.Logger.Information("Starting Api server...");
                    Thread t = new Thread(() =>
                    {
                        ConnectionManager cm = new ConnectionManager(Commands.serverPlayersMap);
                        cm.Start();

                    });
                    t.Start();

                    Log.Logger.Information("Api server started succesfully");

                    //Disconnecting bot from voice channels to prevent errors with DiscordPlayer

                    /*if (args.Length > 0 && args[0].Equals("-i"))
                    {
                        Log.Logger.Warning("Inputs from console are now disabled!");
                        await Task.Delay(-1);
                    }
                    else
                    {
                        await WaitForCommandAsync(DispatchCommandAsync); //This will be awaited forever
                    }*/
                }
                catch (Discord.Net.HttpException)
                {
                    Log.Logger.Fatal("Invalid Discord token.");
                }
            }
        }
    }

    internal interface IDiscordBotService
    {
        public void Run();
    }
}
