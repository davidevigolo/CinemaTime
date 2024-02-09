using BotPollo.Attributes;
using BotPollo.Core;
using BotPollo.SignalR;
using BotPollo.UDP;
using BotPolloG.Grpc;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Serilog;
using SpotifyAPI.Web;
using System.Diagnostics;
using Swan.Logging;

namespace BotPollo
{
    class Program
    {
        //static void Main(string[] args) => new Program().MainAsync(args).GetAwaiter().GetResult(); //Creating never-ending command-waiting process

        public static DiscordSocketClient DiscordClient { get; private set; }
        public static string Token { get; private set; }
        public static DiscordSocketClient GetBot() { return DiscordClient; }
        //public static MongoNode Node { get; private set; }
        public static bool EnableNotifications { get; set; }
        private delegate Task ConsoleCommandAsyncCallback(string content, params string[] args);
        static void Main(string[] args)
        {

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
            Log.Logger.Information(Directory.GetCurrentDirectory());

            var builder = WebApplication.CreateBuilder();
            //BuildConfig(builder.Configuration);
            
            builder.Configuration.SetBasePath(Directory.GetCurrentDirectory());
            builder.Configuration.AddJsonFile("appsettings-dev.json", optional: false, reloadOnChange: true);
            //builder.WebHost.UseKestrel();

            builder.Services.AddSingleton<IDiscordBotService, DiscordBotService>();
            builder.Services.AddSingleton<ICommandManager, CommandManager>();
            builder.Services.AddScoped<IDiscordPlayer, DiscordPlayer>();
            builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
            builder.Services.AddCors();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                Log.Logger.Information(builder.Configuration["Jwt:Key"]);
                Log.Logger.Information(builder.Configuration["Jwt:Audience"]);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey
                    (Convert.FromBase64String(builder.Configuration["Jwt:Key"])),
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];


                        // If the request is for our hub...
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/signalr")))
                        {
                            // Read the token out of the query string
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
            builder.Services.AddAuthorization();
            builder.Host.UseSerilog();
            builder.Services.AddSignalR(o =>
            {
                o.EnableDetailedErrors = true;
            }).AddNewtonsoftJsonProtocol(o => {
                o.PayloadSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                o.PayloadSerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;
            });
            /*.AddJsonProtocol(configure =>
            {
                configure.PayloadSerializerOptions.WriteIndented = false;
                configure.PayloadSerializerOptions.Converters.Clear();
                configure.PayloadSerializerOptions.Converters.Add(new Newtonsoft.Json.JsonSerializer());
            });*/
            /*.AddMessagePackProtocol((options) =>
            {
                options.SerializerOptions = MessagePackSerializerOptions.Standard
                    .WithResolver(StandardResolver.Instance)
                    .WithSecurity(MessagePackSecurity.UntrustedData);
            });*/

            Globals.app = builder.Build();
            var webAppRef = Globals.app as WebApplication;
            
            webAppRef.UseCors(builder =>
            {
                builder.WithOrigins("http://127.0.0.1:5001", "http://127.0.0.1:5173", "https://davidevps.ddns.net")
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .AllowCredentials();
            });
            webAppRef.UseAuthentication();
            webAppRef.UseAuthorization();
            webAppRef.UseHttpsRedirection();
            webAppRef.Urls.Add("http://localhost:5002/");
            webAppRef.MapHub<PlayerHub>("/signalr");
            var svc = Globals.app.Services.GetService<IDiscordBotService>();

            Task.Run(() =>
            {
                svc.Run();
            });

            Globals.app.Run();
        }
        static void BuildConfig(IConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("/appsettings-dev.json", optional: false, reloadOnChange: true)
                //.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }
        /*public async Task MainAsync(string[] args)
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
                Node.Connect();
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
        }*/

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
                Log.Logger.Information("Shutting down bot...");
                Console.ForegroundColor = ConsoleColor.White;
                await DiscordClient.StopAsync();
                Environment.Exit(0);

            }

            if(name == "disconnect" && DiscordClient.ConnectionState != Discord.ConnectionState.Disconnected)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Log.Logger.Warning("Disconnecting bot...");
                Console.ForegroundColor = ConsoleColor.White;
                await DiscordClient.StopAsync();
                Globals.serverPlayersMap.Clear();
                Log.Logger.Information("Bot is now OFFLINE, type connect to turn it back ONLINE");
            }
            else if (name == "disconnect")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Log.Logger.Warning("Bot is already OFFLINE");
                Console.ForegroundColor = ConsoleColor.White;
            }

            if(name == "reconnect" && DiscordClient.ConnectionState != Discord.ConnectionState.Connected)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Log.Logger.Information("Reconnecting bot...");
                Console.ForegroundColor = ConsoleColor.White;
                await DiscordClient.LoginAsync(Discord.TokenType.Bot,Token);
                await DiscordClient.StartAsync();

            }
            else if(name == "reconnect")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Log.Logger.Warning("Bot is already ONLINE");
                Console.ForegroundColor = ConsoleColor.White;
            }

            if(name == "togglenotify")
            {
                EnableNotifications = !EnableNotifications;
                string s = EnableNotifications ? "on" : "off";
                Log.Logger.Information($"Notifications have been turned {s}");
            }
            if(name == "pollo")
            {
                for(int i = 0; i < 20; i++)
                {
                    Log.Logger.Information($"PUNTIIIII");
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

                Log.Logger.Information("Servers active:");
                foreach (var guild in DiscordClient.Guilds)
                {
                    Log.Logger.Information($"{guild.Name} - {guild.MemberCount} Users - {guild.PremiumTier.ToString()}");
                }
            }
        }

        public class DiscordBotService : IDiscordBotService
        {
            private readonly ILogger<DiscordBotService> _logger;
            private readonly IConfiguration _conf;
            private readonly IServiceProvider _serviceProvider;
            public DiscordBotService(ILogger<DiscordBotService> logger, IConfiguration config)
            {
                _logger = logger;
                _conf = config;
                _serviceProvider = Globals.app.Services;
            }
            public async void Run()
            {
                try
                {
                    #region db
                    /*if (args.Length > 0)
                    {
                        Log.Logger.Information($"Launch args: {args[0]}");
                    }*/
                    //MongoDB

                    string dbName = "cinematime";
                    /*Node = new MongoNode("mongodb://localhost", dbName);
                    Node.Log += (string message) => { Logger.Console_Log(message, LogLevel.Database); };
                    Node.Connect();*/
                    #endregion
                    EnableNotifications = true;

                    #region grpcsetup

                    GrpcSetup.GrpcStartup(new string[] { });

                    #endregion
                    //Discord
                    DiscordSocketConfig dSocketConfig = new DiscordSocketConfig();
                    dSocketConfig.GatewayIntents = Discord.GatewayIntents.MessageContent | Discord.GatewayIntents.All;
                    var client = new DiscordSocketClient(dSocketConfig);
                    var service = _serviceProvider.GetService<ICommandManager>();

                    client.SlashCommandExecuted += service.SlashCommandHandler;
                    client.Log += Client_Log;
                    client.UserVoiceStateUpdated += Attributes.CommandManager.UserJoinedVChannelHandlerAsync;
                    client.ButtonExecuted += Commands.HandleButtonInteractionAsync; //Add to ICommandManager
                    client.Ready += async () => {
                        service.RegisterCommands<Commands>();
                    };

                    Token = File.ReadAllText("token.txt"); //It's already expired of course
                    await client.LoginAsync(Discord.TokenType.Bot, Token);
                    await client.StartAsync();
                    DiscordClient = client;

                    //Spotify

                    var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new ClientCredentialsAuthenticator("5b60c795a9874f5b943362f0020b47d4", "99516aca0c26435398e543831e0c7113"));
                    Globals.spotifyClient = new SpotifyClient(config);

                    //SpotifyEnd

                    Log.Logger.Information("Starting Api server...");
                    Thread t = new Thread(() =>
                    {
                        IConnectionManager cm = Globals.app.Services.GetRequiredService<IConnectionManager>();
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

            private async Task Client_Log(Discord.LogMessage arg)
            {
                Log.Logger.Information(arg.Message,arg);
            }
        }
    }

    internal interface IDiscordBotService
    {
        public void Run();
    }
}
