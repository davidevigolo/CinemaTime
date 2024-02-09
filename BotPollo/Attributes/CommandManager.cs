using BotPollo.Core;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reflection;
using BotPollo.SignalR;
using Microsoft.AspNetCore.SignalR;
using Serilog.Core;

namespace BotPollo.Attributes
{
    class CommandManager : ICommandManager
    {
        private readonly ILogger<CommandManager> _logger;
        private Dictionary<string, MethodInfo> commandMap = new Dictionary<string, MethodInfo>();
        private static IHubContext<PlayerHub> _hubContext;
        public delegate void UserVoiceChannelUpdate(ulong uuid, IVoiceState newState, IVoiceState oldstate);
        public static event UserVoiceChannelUpdate UserVoiceUpdate;
        public CommandManager(ILogger<CommandManager> logger, IHubContext<PlayerHub> context)
        {
            _logger = logger;
            _hubContext = context;
        }

        public void RegisterCommands<T>()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var types = from t in typeof(T).GetMethods() where t.GetCustomAttributes<Command>().Count() > 0 select t;

            foreach (MethodInfo mi in types)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                _logger.LogInformation("Command: " + ((Command)mi.GetCustomAttribute(typeof(Command))).Name + " binded to method: " + mi.Name);
                var aliases = ((Command)mi.GetCustomAttribute(typeof(Command))).aliases;
                foreach(string alias in aliases)
                {
                    commandMap.Add(alias, mi);
                    _logger.LogInformation("Alias: " + alias + " binded to method: " + mi.Name);
                }
                commandMap.Add(((Command)mi.GetCustomAttribute(typeof(Command))).Name.ToLower(), mi);

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
                    _logger.LogError("Error registering slash command");
                }
            }

            foreach(KeyValuePair<string, MethodInfo> kvp in commandMap)
            {
                _logger.LogInformation($"{kvp.Key} => {kvp.Value.Name}()");
            }

            sw.Stop();

            _logger.LogInformation($"Setup finished in {sw.ElapsedMilliseconds}ms");
        }

        public static async Task UserJoinedVChannelHandlerAsync(SocketUser user, SocketVoiceState state,
            SocketVoiceState state2)
        {
            //if (state.VoiceChannel == state2.VoiceChannel || !Program.EnableNotifications) return;
            if (user.Id == Program.GetBot().CurrentUser.Id) return;
            Console.WriteLine("Event sent");
            ulong uuid = user.Id;
            if (uuid == Program.GetBot().CurrentUser.Id) return;
            try
            {
                string connectionId = PlayerHub._states[uuid].ConnectionId;
                ulong playerId = (await Globals.GetUserActivePlayer(uuid))[0];
                IDiscordPlayer player = Globals.serverPlayersMap[playerId];
                if (state.VoiceChannel != null)
                {
                    _hubContext.Groups.RemoveFromGroupAsync($"{connectionId}", state.VoiceChannel.Guild.Id.ToString());
                }

                if (state2.VoiceChannel != null)
                {
                    Console.WriteLine($"Event received, adding user {connectionId} to group {playerId.ToString()}");
                    await _hubContext.Groups.AddToGroupAsync($"{connectionId}", playerId.ToString());
                    await _hubContext.Clients.Client(connectionId).SendAsync("PlayerUpdate", player.GetPlayerStatus());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return;
            }
        }

        public async Task CommandHandler(SocketMessage msg)
        {
            if (commandMap.ContainsKey(msg.Content.Split(' ')[0].ToLower())) //Split serve a prendere la parte del messaggio contenente il nome del comando
            {
                var method = commandMap.GetValueOrDefault(msg.Content.Split(' ')[0].ToLower());
                method.Invoke(null, new object[] { msg });
                Console.ForegroundColor = ConsoleColor.Cyan;
                _logger.LogInformation("User: " + msg.Author.Username + " Used command: " + ((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower());
                /*await MongoIO.InsertJSONAsync(Program.Node.GetBsonCollection("bot_logs"), new
                {
                    user_id = msg.Author.Id,
                    action = ((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower(),
                    event_id = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower()))),
                    time = DateTime.Now.ToString(),
                    message_thread = msg.Thread.Id,
                    message_content = msg.Content,
                    interaction = msg.Interaction.Id,
                    interaction_name = msg.Interaction.Name,
                    source_type = msg.Source.GetType().Name,
                    channel_id = msg.Channel.Id
                }.ToBsonDocument());*/
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public async Task SlashCommandHandler(SocketSlashCommand command)
        {
            if (commandMap.ContainsKey(command.CommandName)) //Split serve a prendere la parte del messaggio contenente il nome del comando
            {
                var method = commandMap.GetValueOrDefault(command.CommandName);
                method.Invoke(null, new object[] { command });

                Console.ForegroundColor = ConsoleColor.Cyan;
                //Logger.Console_Log("User: " + command.User.Username + " Used slash command: " + ((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower(), LogLevel.Info);
                //Logger.Console_Log("Channel:" + command.Channel.Name + " Id:" + command.ChannelId, LogLevel.Info);
                //Logger.Console_Log("Data:" + command.Data, LogLevel.Info);
                Serilog.Log.Logger.Information<SocketSlashCommand>("Command used", command);
                List<object> objects = new List<object>();
                foreach (var obj in command.Data.Options)
                {
                    objects.Add(new
                    {
                        name = obj.Name,
                        value = obj.Value
                    });
                }
                try
                {
                    /*await MongoIO.InsertJSONAsync(Program.Node.GetBsonCollection("bot_logs"), new
                    {
                        user_id = command.Id,
                        action = ((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower(),
                        event_id = Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(((Command)method.GetCustomAttribute(typeof(Command))).Name.ToLower()))),
                        time = DateTime.Now.ToString(),
                        interaction = command.Type.ToString(),
                        message_thread = command.Id,
                        channel_id = command.Channel.Id,
                        arguments = objects.ToArray()
                    }.ToBsonDocument());*/
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, ex);
                    Serilog.Log.Logger.Error(ex, ex.Message);
                }
                Console.ForegroundColor = ConsoleColor.White;
            }
        }
    }

    internal interface ICommandManager
    {
        public void RegisterCommands<T>();
        public Task CommandHandler(SocketMessage message);
        public Task SlashCommandHandler(SocketSlashCommand command);
    }
}
