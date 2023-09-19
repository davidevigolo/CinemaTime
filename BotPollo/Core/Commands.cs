using BotPollo.Attributes;
using BotPollo.Core.Exceptions;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Net;
using static BotPollo.Core.DiscordPlayer;

namespace BotPollo.Core
{
    public class Commands : ICommandModule
    {
        private static ILogger _logger;


        [Command("ping")]
        public static async Task CheckPunti(SocketMessage msg)
        {
            var channel = msg.Channel;
            int ping = Program.GetBot().Latency;
            await channel.SendMessageAsync($":ping_pong: pong! {ping}ms");
        }

        [Command("invito")]
        public static async Task InviteMessage(SocketSlashCommand ssc)
        {
            var channel = ssc.Channel;
            Discord.EmbedBuilder builder = new Discord.EmbedBuilder()
            {
                Title = "Click here to add the bot to your server",
                Description = $"https://discord.com/api/oauth2/authorize?client_id={Program.GetBot().CurrentUser.Id}&permissions=8&scope=bot",
                Color = Discord.Color.Blue,
                ThumbnailUrl = "https://cdn.discordapp.com/avatars/885174574591930448/e840f87959f3b280e335d78e02263cfa.webp?size=128"
            };

            await ssc.RespondAsync("",embed: builder.Build());
        }

        [Command("creasondaggio")]
        public static async Task CreatePoll(SocketMessage msg)
        {
            var firstMessage = msg;
            Dialogue dial = new Dialogue(msg.Author, msg.Channel, 20000);
            dial.Timeout += async () => {
                await dial.ReplyAsync("Comando annullato",true,true);
                await Task.Delay(1300);
                await firstMessage.DeleteAsync();
                await dial.ClearMessageCacheAsync();
                dial.Close(); };
            await dial.ReplyAsync("Inserisci il titolo",false,false);
            var result = await dial.GetUserReplyAsync(false,false,true);
            if (result != null)
            {
                await dial.ReplyAsync("Inserisci la descrizione",true,true);
                var resp = await dial.GetUserReplyAsync(false, false, true);
                if (resp != null)
                {
                    await dial.ReplyAsync("Attendi...", true,true);
                    var embed = new EmbedBuilder
                    {
                        Title = result.Content,
                        Description = resp.Content,
                    }.WithAuthor(new EmbedAuthorBuilder
                    {
                        Name = msg.Author.Username,
                        IconUrl = msg.Author.GetAvatarUrl()
                    }).WithColor(Discord.Color.Blue)
                    .WithFooter("CinemaTime™")
                    .WithTimestamp(DateTime.Now)
                    .WithThumbnailUrl("https://thumbs.dreamstime.com/b/poll-vector-line-icon-outline-concept-linear-sign-symbol-147159048.jpg");

                    await msg.Channel.SendMessageAsync(embed: embed.Build());
                    await dial.ClearMessageCacheAsync();
                    dial.Close();
                    await firstMessage.DeleteAsync();
                }
            }
        }

        [Command("getmeta")]
        public static async Task RequestURL(SocketMessage msg)
        {
            await msg.Channel.SendMessageAsync("Inoltrando la richiesta...");
            string content = msg.Content;
            string id = content.Split(' ')[1];

            using (WebClient client = new WebClient())
            {
                var temp = client.DownloadString($"http://youtube.com/get_video_info?video_id={id}");
                await msg.Channel.SendMessageAsync(temp);
            }
        }

        [Command("disconnect")]
        public static async Task Disconnect(SocketSlashCommand ssc)
        {
            IDiscordPlayer player = Globals.serverPlayersMap[(ulong)ssc.GuildId];
            player.Dispose();
        }

        [Command("skip")]
        public static async Task Skip(SocketSlashCommand ssc)
        {
            await ssc.DeferAsync();
            try
            {
                if (await AssertUserInSameVChannel(ssc))
                {
                    IDiscordPlayer player = await GetServerDiscordPlayer((ssc.User as IGuildUser).GuildId);
                    player.Skip();
                    await ssc.FollowupAsync("Song skipped!", ephemeral: true);
                    await Task.Run(() =>
                    {
                        Task.Delay(4000);
                        ssc.DeleteOriginalResponseAsync();
                    });
                }
            }catch(DiscordPlayerNotConnectedException)
            {
                await ssc.FollowupAsync("The bot isn't connected on this server at the moment", ephemeral: true);
            }
        }

        [Command("time")]
        public static async Task Time(SocketSlashCommand ssc)
        {
            await ssc.DeferAsync();
            IDiscordPlayer player = Globals.serverPlayersMap[(ulong)ssc.GuildId];
            if (!player.isPlaying)
            {
                await ssc.RespondAsync("The bot isn't playing any song at the moment or it is not connected at all");
                return;
            }

            string pollo = player.GetTime().ToString();
            await ssc.RespondAsync(pollo);

        }

        [Command("play","p","pla","pl")]
        [Option("query",ApplicationCommandOptionType.String,true)]
        public static async Task CanzonePollo(SocketSlashCommand ssc)
        {
            await ssc.DeferAsync();
            RestUserMessage message;
            /*if(Songs.Count == 0)
            {
                string[] files = Directory.GetFiles(@"C:\Users\Administrator\Music\");
                if (Songs.Count == 0 || files.Length > Songs.Count)
                {
                    Songs.Clear();
                    foreach (string dir in files)
                    {
                        Songs.Add(dir.Split('\\').Last());
                    }

                }
            }
            int songIndex;*/
            string q = "";
            if (ssc.Data.Options.First().Value == " ")
            {
                /*Dialogue dial = new Dialogue(msg.Author, msg.Channel, 30000);

                string message = "";
                for (int i = 0; i < Songs.Count; i++)
                {
                    message += $"{i} • " + Songs[i] + "\n";
                }
                //try
                //{
                //    EmbedBuilder embed = new EmbedBuilder
                //    {
                //        Title = "Canzoni tantrificanti: ",
                //        Description = message
                //    }.WithAuthor(new EmbedAuthorBuilder
                //    {
                //        Name = Program.GetBot().CurrentUser.Username,
                //        IconUrl = Program.GetBot().CurrentUser.GetAvatarUrl()
                //    });
                //    await msg.Channel.SendMessageAsync(embed: embed.Build());
                //}
                //catch (Exception exp)
                //{
                //    Logging.Logger.Console_Log(exp.StackTrace, Logging.LogLevel.Error);
                //    Logging.Logger.Console_Log(exp.Message, Logging.LogLevel.Error);
                //}
                await msg.Channel.SendMessageAsync($"`{message}`");
                var response = await dial.GetUserReplyAsync(false, false, false);
                songIndex = Int32.Parse(response.Content);
                dial.Close();*/
                var color = Program.GetBot().GetGuild((ulong)ssc.GuildId).GetUser(ssc.User.Id).Roles.OrderByDescending(x => x.Position).First().Color;
                EmbedBuilder embed = new EmbedBuilder
                {
                    Title = "Usage",
                    Description = "pollo <song name>",
                    Color = Program.GetBot().GetGuild((ulong)ssc.GuildId).GetUser(ssc.User.Id).Roles.OrderByDescending(x => x.Position).First().Color
                }.WithAuthor(new EmbedAuthorBuilder
                {
                    Name = Program.GetBot().CurrentUser.Username,
                    IconUrl = Program.GetBot().CurrentUser.GetAvatarUrl()
                });
                await ssc.Channel.SendMessageAsync(embed: embed.Build());
                return;
            }
            else
            {
                q = ssc.Data.Options.First().Value.ToString();
            }
            if ((ssc.User as IGuildUser).VoiceChannel != null && (ssc.Channel as IGuildChannel).GuildId == (ssc.User as IGuildUser).VoiceChannel.GuildId)
            {
                IVoiceChannel voiceChannel = (ssc.User as IGuildUser).VoiceChannel;

                if (!Globals.serverPlayersMap.ContainsKey(voiceChannel.GuildId))
                {
                    try
                    {
                        var audioClient = await voiceChannel.ConnectAsync(); //Inserisci handling delle troppe persone nel canale
                        //var playerService = Globals.app.Services.GetRequiredService<IDiscordPlayer>();
                        DiscordPlayer player = ActivatorUtilities.CreateInstance(Globals.app.Services, typeof(DiscordPlayer), audioClient, ssc.Channel as IMessageChannel, voiceChannel, Globals.spotifyClient) as DiscordPlayer;
                        //DiscordPlayer player = new DiscordPlayer(audioClient, ssc.Channel as IMessageChannel, voiceChannel);
                        Globals.serverPlayersMap.Add(voiceChannel.GuildId, player);
                        player.NewSongPlaying += Player_NewSongPlaying; //SISTEMA EVENTI CHE NON TRIGGERANO
                        player.SongAdded += Player_SongAdded;
                        player.PlaylistAdded += Player_PlaylistAdded;
                        player.PlayerDestroyed += Player_PlayerDestroyed;
                    }
                    catch (Exception ex)
                    {
                        Logging.Logger.Console_Log(ex.Message, Logging.LogLevel.Error);
                    }
                }
                try
                {
                    if (await AssertUserInSameVChannel(ssc))
                    {
                        IDiscordPlayer player = await GetServerDiscordPlayer((ssc.User as IGuildUser).GuildId);
                        var res = await player.AddSongAsync(q);
                        if (!res)
                        {

                            await ssc.FollowupAsync($":red_square: Song not found!", ephemeral: true);
                            return;
                        }
                        await ssc.FollowupAsync("‎ ", ephemeral: true);
                        await ssc.DeleteOriginalResponseAsync();
                        return;
                    }
                    await ssc.FollowupAsync(":red_square: You're not in the same voice channel of the bot", ephemeral: true);
                }
                catch(DiscordPlayerNotConnectedException ex)
                {
                    await ssc.FollowupAsync(":red_square: The bot isn't connected on this server at the moment", ephemeral: true);
                }
            }
            else
            {
                await ssc.FollowupAsync(":red_square: You're not in a valid voice channel", ephemeral: true);
            }
        }
        private static void Player_PlayerDestroyed(ulong guildId)
        {
            Globals.serverPlayersMap.Remove(guildId);
            Logging.Logger.Console_Log($"Player {guildId} has been disposed", Logging.LogLevel.Info);
        }

        [Command("seek")]
        [Option("timestamp",ApplicationCommandOptionType.String,true)]
        public static async Task SeekSongAsync(SocketSlashCommand ssc)
        {
            ssc.DeferAsync();
            string arg = ssc.Data.Options.First().Value.ToString();
            if(arg.Count(x => x == ':') < 2)
            {
                arg = $"00:{arg}";
            }
            TimeSpan inSec = TimeSpan.Parse(arg);
            try
            {
                if (await AssertUserInSameVChannel(ssc))
                {
                    IDiscordPlayer player = await GetServerDiscordPlayer((ulong)ssc.GuildId);
                    await player.SeekAsync(inSec);
                    await ssc.FollowupAsync("Track resuming at: " + inSec.ToString(),ephemeral: true);
                    await Task.Run(() =>
                    {
                        Task.Delay(4000);
                        ssc.DeleteOriginalResponseAsync();
                    });
                    return;
                }
                await ssc.FollowupAsync("You're not in the same voice channel of the bot",ephemeral: true);
            }
            catch (DiscordPlayerNotConnectedException ex)
            {
                await ssc.FollowupAsync("The bot isn't connected on this server at the moment", ephemeral: true);
            }
        }

        [Command("speed","s")]
        [Option("factor",ApplicationCommandOptionType.Number,true)]
        public static async Task SetSpeedAsync(SocketSlashCommand ssc)
        {
            string arg = ssc.Data.Options.First().Value.ToString();
            double speed = Convert.ToDouble(arg);
            try
            {
                if (await AssertUserInSameVChannel(ssc))
                {
                    IDiscordPlayer player = await GetServerDiscordPlayer((ulong)ssc.GuildId);
                    await player.SetSpeedAsync(speed);
                    return;
                }
                await ssc.RespondAsync("You're not in the same voice channel of the bot");
            }
            catch (DiscordPlayerNotConnectedException ex)
            {
                await ssc.RespondAsync("The bot isn't connected on this server at the moment");
            }
        }
        [Command("pitch","key")]
        [Option("factor",ApplicationCommandOptionType.String,true)]
        public static async Task SetPitchAsync(SocketSlashCommand ssc)
        {
            await ssc.DeferAsync();
            string arg = ssc.Data.Options.First().Value.ToString();
            try
            {
                if (await AssertUserInSameVChannel(ssc))
                {
                    IDiscordPlayer player = await GetServerDiscordPlayer((ulong)ssc.GuildId);
                    await player.ChangePitchAsync(arg);
                    await ssc.FollowupAsync("Pitch changed!");
                    await ssc.DeleteOriginalResponseAsync();
                    return;
                }
                await ssc.FollowupAsync("You're not in the same voice channel of the bot",ephemeral: true);
            }
            catch (DiscordPlayerNotConnectedException ex)
            {
                await ssc.FollowupAsync("The bot isn't connected on this server at the moment",ephemeral: true);
            }
        }
        [Command("pause")]
        public async static Task Player_Pause(SocketSlashCommand ssc)
        {
            if (ssc.GuildId is null) ssc.RespondAsync("You need to execute this command in a guild");
            var player = Globals.serverPlayersMap[(ulong)ssc.GuildId];
            player.Pause();
            await ssc.RespondAsync();
            await ssc.DeleteOriginalResponseAsync();
        }
        [Command("resume")]
        public async static Task Player_Resume(SocketSlashCommand ssc)
        {
            if (ssc.GuildId is null) ssc.RespondAsync("You need to execute this command in a guild");
            var player = Globals.serverPlayersMap[(ulong)ssc.GuildId];
            player.Resume();
            await ssc.RespondAsync();
            await ssc.DeleteOriginalResponseAsync();
        }
        private async static void Player_SongAdded(string name, IMessageChannel commandChannel, DiscordPlayer dp)
        {
            var guildId = Globals.serverPlayersMap.First(x => x.Value == dp).Key;
            var color = Program.GetBot().GetGuild(guildId).GetUser(Program.GetBot().CurrentUser.Id).Roles.OrderByDescending(x => x.Position).FirstOrDefault().Color;
            var url = dp.CurrentQueueObject.VideoInfo.Thumbnails.FirstOrDefault().Url;

            var buttons = new ComponentBuilder();
            buttons.WithButton("Boost bass", style: ButtonStyle.Primary, customId: "1");
            buttons.WithButton("Boost mids", style: ButtonStyle.Primary, customId: "2");
            buttons.WithButton("Boost treble", style: ButtonStyle.Primary, customId: "3");
            buttons.WithButton("Clean sound", style: ButtonStyle.Primary, customId: "4");
            buttons.WithButton("8D", style: ButtonStyle.Primary, customId: "5");
            buttons.WithButton("Lowpass", style: ButtonStyle.Primary, customId: "6");
            buttons.WithButton("Lyrics", style: ButtonStyle.Secondary, customId: "100");
            if (!dp.HasChannelMessage())
            {
                dp.PlayerMessage = await commandChannel.SendMessageAsync(embed: embedString("Now playing", $"[{dp.CurrentQueueObject.VideoInfo.Title}]({dp.CurrentQueueObject.VideoInfo.Title})", dp.CurrentQueueObject.StreamInfo.Bitrate.KiloBitsPerSecond, dp.CurrentQueueObject.VideoInfo.Author.ChannelTitle, (TimeSpan)dp.CurrentQueueObject.VideoInfo.Duration, color, url), components: buttons.Build());
            }
            var originalEmbed = dp.PlayerMessage.Embeds.First().ToEmbedBuilder();
            string embedValue = "";
            foreach (QueueObject obj in dp.SongQueue)
            {
                embedValue += obj.VideoInfo.Title + "\r\n";
            }
            if (originalEmbed.Fields.Where(x => x.Name == "Queue").Count() != 0)
                originalEmbed.Fields.Remove(originalEmbed.Fields.Where(x => x.Name == "Queue").First());
            originalEmbed.WithFields(new EmbedFieldBuilder() { Name = "Queue", Value = embedValue });
            dp.PlayerMessage.ModifyAsync(x => x.Embed = originalEmbed.Build());
        }

        private async static void Player_NewSongPlaying(string name, IMessageChannel commandChannel, DiscordPlayer dp)
        {
            var guildId = Globals.serverPlayersMap.First(x => x.Value == dp).Key;
            var color = Program.GetBot().GetGuild(guildId).GetUser(Program.GetBot().CurrentUser.Id).Roles.OrderByDescending(x => x.Position).FirstOrDefault().Color;
            var url = dp.CurrentQueueObject.VideoInfo.Thumbnails.FirstOrDefault().Url;

            var buttons = new ComponentBuilder();
            buttons.WithButton("Boost bass", style: ButtonStyle.Primary, customId: "1");
            buttons.WithButton("Boost mids", style: ButtonStyle.Primary, customId: "2");
            buttons.WithButton("Boost treble", style: ButtonStyle.Primary, customId: "3");
            buttons.WithButton("Clean sound", style: ButtonStyle.Primary, customId: "4");
            buttons.WithButton("8D", style: ButtonStyle.Primary, customId: "5");
            buttons.WithButton("Lowpass", style: ButtonStyle.Primary, customId: "6");
            buttons.WithButton("Lyrics", style: ButtonStyle.Secondary, customId: "100");
            if (!dp.HasChannelMessage())
            {
                dp.PlayerMessage = await commandChannel.SendMessageAsync(embed: embedString("Now Playing", $"[{dp.CurrentQueueObject.VideoInfo.Title}]({dp.CurrentQueueObject.VideoInfo.Url})", dp.CurrentQueueObject.StreamInfo.Bitrate.KiloBitsPerSecond, dp.CurrentQueueObject.VideoInfo.Author.ChannelTitle, (TimeSpan)dp.CurrentQueueObject.VideoInfo.Duration, color, url), components: buttons.Build());
            }
            var originalEmbed = dp.PlayerMessage.Embeds.First().ToEmbedBuilder();
            string embedValue = "";
            foreach (QueueObject obj in dp.SongQueue)
            {
                embedValue += obj.VideoInfo.Title + "\r\n";
            }

            originalEmbed.Description = $"{name}";
            originalEmbed.Color = color;
            originalEmbed.ThumbnailUrl = url;
            originalEmbed.WithAuthor(Program.GetBot().CurrentUser);

            originalEmbed.Fields.Clear();
            originalEmbed.WithFields(new EmbedFieldBuilder().WithName("Bitrate").WithValue(Math.Truncate(dp.CurrentQueueObject.StreamInfo.Bitrate.KiloBitsPerSecond * 100) / 100 + "kb/s").WithIsInline(true));
            originalEmbed.WithFields(new EmbedFieldBuilder().WithName("Author").WithValue(dp.CurrentQueueObject.VideoInfo.Author.ChannelTitle).WithIsInline(true));
            originalEmbed.WithFields(new EmbedFieldBuilder().WithName("Video duration").WithValue(dp.CurrentQueueObject.VideoInfo.Duration).WithIsInline(true));
            if (embedValue != "")
                originalEmbed.WithFields(new EmbedFieldBuilder() { Name = "Queue", Value = embedValue });
            originalEmbed.WithTimestamp(DateTime.Now);

            dp.PlayerMessage.ModifyAsync(x => x.Embed = originalEmbed.Build());
        }
        private async static void Player_PlaylistAdded(string[] names, IMessageChannel commandChannel, DiscordPlayer dp)
        {
            var guildId = Globals.serverPlayersMap.First(x => x.Value == dp).Key;
            var color = Program.GetBot().GetGuild(guildId).GetUser(Program.GetBot().CurrentUser.Id).Roles.OrderByDescending(x => x.Position).FirstOrDefault().Color;
            var url = dp.CurrentQueueObject.VideoInfo.Thumbnails.FirstOrDefault().Url;

            var buttons = new ComponentBuilder();
            buttons.WithButton("Boost bass", style: ButtonStyle.Primary, customId: "1");
            buttons.WithButton("Boost mids", style: ButtonStyle.Primary, customId: "2");
            buttons.WithButton("Boost treble", style: ButtonStyle.Primary, customId: "3");
            buttons.WithButton("Clean sound", style: ButtonStyle.Primary, customId: "4");
            buttons.WithButton("8D", style: ButtonStyle.Primary, customId: "5");
            buttons.WithButton("Lowpass", style: ButtonStyle.Primary, customId: "6");
            buttons.WithButton("Lyrics", style: ButtonStyle.Secondary, customId: "100");
            if (!dp.HasChannelMessage())
            {
                dp.PlayerMessage = await commandChannel.SendMessageAsync(embed: embedString("Now playing", $"[{dp.CurrentQueueObject.VideoInfo.Title}]({dp.CurrentQueueObject.VideoInfo.Url})", dp.CurrentQueueObject.StreamInfo.Bitrate.KiloBitsPerSecond, dp.CurrentQueueObject.VideoInfo.Author.ChannelTitle, (TimeSpan)dp.CurrentQueueObject.VideoInfo.Duration, color, url), components: buttons.Build());
            }

            var originalEmbed = dp.PlayerMessage.Embeds.First().ToEmbedBuilder();
            string embedValue = "";
            foreach (QueueObject obj in dp.SongQueue)
            {
                if ((embedValue + obj.VideoInfo.Title + "\r\n").Length > 1024) break;
                embedValue += obj.VideoInfo.Title + "\r\n";
            }
            if (originalEmbed.Fields.Where(x => x.Name == "Queue").Count() != 0)
                originalEmbed.Fields.Remove(originalEmbed.Fields.Where(x => x.Name == "Queue").First());
            originalEmbed.WithFields(new EmbedFieldBuilder() { Name = "Queue", Value = embedValue });
            dp.PlayerMessage.ModifyAsync(x => x.Embed = originalEmbed.Build());
        }




        private static Embed embedString(string action,string song,double bitrate, string author, TimeSpan duration,Discord.Color color,string thumbnailurl = null)
        {
            if(color == null)
            {
                color = Discord.Color.Blue;
            }
            var botClient = Program.GetBot();
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = action,
                Description = $"{song}",
                Color = color,
                ThumbnailUrl = thumbnailurl
            }
            .WithTimestamp(DateTime.Now);
            embed.Fields.Add(new EmbedFieldBuilder().WithName("Bitrate").WithValue(Math.Truncate(bitrate * 100) / 100 + "kb/s").WithIsInline(true));
            embed.Fields.Add(new EmbedFieldBuilder().WithName("Author").WithValue(author).WithIsInline(true));
            embed.Fields.Add(new EmbedFieldBuilder().WithName("Video duration").WithValue(duration.ToString()).WithIsInline(true));
            return embed.Build();
        }

        private static async Task<bool> AssertUserInSameVChannel(SocketMessage msg)
        {
            IGuildUser sender = (IGuildUser)msg.Author;
            IGuildChannel channel = (IGuildChannel)msg.Channel;
            if (Globals.serverPlayersMap.ContainsKey(channel.GuildId))
            {
                var usersInVoiceChannel = await Globals.serverPlayersMap.GetValueOrDefault(channel.GuildId).AudioChannel.GetUserAsync(sender.Id);
                if (usersInVoiceChannel == null)
                    return false;
                return true;
            }
            throw new DiscordPlayerNotConnectedException();
        }

        private static async Task<bool> AssertUserInSameVChannel(SocketSlashCommand ssc)
        {
            IGuildUser sender = (IGuildUser)ssc.User;
            IGuildChannel channel = (IGuildChannel)ssc.Channel;
            if (Globals.serverPlayersMap.ContainsKey(channel.GuildId))
            {
                var usersInVoiceChannel = await Globals.serverPlayersMap.GetValueOrDefault(channel.GuildId).AudioChannel.GetUserAsync(sender.Id);
                if (usersInVoiceChannel == null)
                    return false;
                return true;
            }
            throw new DiscordPlayerNotConnectedException();
        }
        public static async Task HandleButtonInteractionAsync(SocketMessageComponent smc)
        {

            switch(smc.Data.CustomId)
            {
                case "1":
                    var player = Globals.serverPlayersMap[(ulong)smc.GuildId];
                    player.ChangeFiltersAsync(0);
                    await smc.RespondAsync();
                    break;
                case "2":
                    var player2 = Globals.serverPlayersMap[(ulong)smc.GuildId];
                    player2.ChangeFiltersAsync(1);
                    await smc.RespondAsync();
                    break;
                case "3":
                    var player3 = Globals.serverPlayersMap[(ulong)smc.GuildId];
                    player3.ChangeFiltersAsync(2);
                    await smc.RespondAsync();
                    break;
                case "4":
                    var player4 = Globals.serverPlayersMap[(ulong)smc.GuildId];
                    player4.ChangeFiltersAsync(3);
                    await smc.RespondAsync();
                    break;
                case "5":
                    var player5 = Globals.serverPlayersMap[(ulong)smc.GuildId];
                    player5.ChangeFiltersAsync(4);
                    await smc.RespondAsync();
                    break;
                case "6":
                    var player6 = Globals.serverPlayersMap[(ulong)smc.GuildId];
                    player6.ChangeFiltersAsync(5);
                    await smc.RespondAsync();
                    break;
                case "100":
                    await smc.DeferAsync();
                    var player100 = Globals.serverPlayersMap[(ulong)smc.GuildId];
                    try
                    {
                        string lyrics = await player100.GetLyrics();
                        player100.LyricsMessage = await smc.FollowupAsync(lyrics);
                    }
                    catch (ArgumentException ex)
                    {
                        await smc.FollowupAsync(":red_square: No lyrics found!");
                    }
                    break;
            }
        } 
        public static async Task<IDiscordPlayer> GetServerDiscordPlayer(ulong guildId) => Globals.serverPlayersMap.GetValueOrDefault(guildId);
    }

    public interface ICommandModule
    {
        //Free for future implementation
    }
}
