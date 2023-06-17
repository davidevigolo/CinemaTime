using BotPollo.Attributes;
using BotPollo.Core.Exceptions;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Core
{
    public class Commands
    {
        public static Dictionary<ulong, DiscordPlayer> serverPlayersMap = new Dictionary<ulong, DiscordPlayer>();

        [Command("ping")]
        public static async Task CheckPunti(SocketMessage msg)
        {
            var channel = msg.Channel;
            int ping = Program.GetBot().Latency;
            await channel.SendMessageAsync($":ping_pong: pong! {ping}ms");
        }

        [Command("invito")]
        public static async Task InviteMessage(SocketMessage msg)
        {
            var channel = msg.Channel;
            Discord.EmbedBuilder builder = new Discord.EmbedBuilder()
            {
                Title = "Click here to add the bot to your server",
                Description = "https://discord.com/api/oauth2/authorize?client_id=885174574591930448&permissions=8&scope=bot",
                Color = Discord.Color.Blue,
                ThumbnailUrl = "https://cdn.discordapp.com/avatars/885174574591930448/e840f87959f3b280e335d78e02263cfa.webp?size=128"
            };

            await channel.SendMessageAsync("", false, builder.Build());
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

       /* [Command("p")]
        public static async Task YoutubeSongAsync(SocketMessage msg)
        {
            string content = msg.Content;
            string q = msg.Content.Substring(content.IndexOf('p') + 1);
            IVoiceChannel voiceChannel = (msg.Author as IGuildUser).VoiceChannel;
            var audioClient = await voiceChannel.ConnectAsync();
            player = new DiscordPlayer(audioClient, msg.Channel as IMessageChannel,voiceChannel);
            await player.AddYoutubeSongAsync(q);
        }*/

        [Command("skip")]
        public static async Task Skip(SocketSlashCommand ssc)
        {
            try
            {
                if (await AssertUserInSameVChannel(ssc))
                {
                    DiscordPlayer player = await GetServerDiscordPlayer((ssc.User as IGuildUser).GuildId);
                    player.Skip();
                    await ssc.RespondAsync("Song skipped!");
                    await Task.Run(() =>
                    {
                        Task.Delay(4000);
                        ssc.DeleteOriginalResponseAsync();
                    });
                }
            }catch(DiscordPlayerNotConnectedException)
            {
                await ssc.RespondAsync("The bot isn't connected on this server at the moment");
            }
        }

        [Command("time")]
        public static async Task Time(SocketSlashCommand ssc)
        {
            DiscordPlayer player = serverPlayersMap[(ulong)ssc.GuildId];
            if (!player.isPlaying)
            {
                await ssc.RespondAsync("The bot isn't playing any song at the moment or it is not connected at all");
                return;
            }

            string pollo = player.GetTime().ToString();
            await ssc.RespondAsync(pollo);

        }

        [Command("p","play","pla","pl")]
        [Option("query",ApplicationCommandOptionType.String,true)]
        public static async Task CanzonePollo(SocketSlashCommand ssc)
        {
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
                EmbedBuilder embed = new EmbedBuilder
                {
                    Title = "Usage",
                    Description = "pollo <song name>"
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

                if (!serverPlayersMap.ContainsKey(voiceChannel.GuildId))
                {
                    try
                    {
                        var audioClient = await voiceChannel.ConnectAsync();
                        DiscordPlayer player = new DiscordPlayer(audioClient, ssc.Channel as IMessageChannel, voiceChannel);
                        serverPlayersMap.Add(voiceChannel.GuildId, player);
                        player.NewSongPlaying += Player_NewSongPlaying; //SISTEMA EVENTI CHE NON TRIGGERANO
                        player.SongAdded += Player_SongAdded;
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
                        DiscordPlayer player = await GetServerDiscordPlayer((ssc.User as IGuildUser).GuildId);
                        var res = await player.AddYoutubeSongAsync(q);
                        if (!res)
                        {

                            await ssc.RespondAsync($":red_square: Song not found!");
                            return;
                        }

                        await ssc.RespondAsync();
                        return;
                    }
                    await ssc.RespondAsync(":red_square: You're not in the same voice channel of the bot");
                }
                catch(DiscordPlayerNotConnectedException ex)
                {
                    await ssc.RespondAsync(":red_square: The bot isn't connected on this server at the moment");
                }
            }
            else
            {
                await ssc.RespondAsync(":red_square: You're not in a valid voice channel");
            }
        }

        [Command("seek")]
        [Option("timestamp",ApplicationCommandOptionType.String,true)]
        public static async Task SeekSongAsync(SocketSlashCommand ssc)
        {
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
                    DiscordPlayer player = await GetServerDiscordPlayer((ulong)ssc.GuildId);
                    await player.SeekAsync(inSec);
                    await ssc.RespondAsync("Track resuming at: " + inSec.ToString());
                    await Task.Run(() =>
                    {
                        Task.Delay(4000);
                        ssc.DeleteOriginalResponseAsync();
                    });
                    return;
                }
                await ssc.RespondAsync("You're not in the same voice channel of the bot");
            }
            catch (DiscordPlayerNotConnectedException ex)
            {
                await ssc.RespondAsync("The bot isn't connected on this server at the moment");
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
                    DiscordPlayer player = await GetServerDiscordPlayer((ulong)ssc.GuildId);
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
        private static void Player_SongAdded(string name, IMessageChannel commandChannel,DiscordPlayer dp)
        {
            commandChannel.SendMessageAsync(embed: embedString("Added to queue",name,dp.currentStreamInfo.Bitrate.KiloBitsPerSecond,dp.currentVideoInfo.Author.ChannelTitle, (TimeSpan)dp.currentVideoInfo.Duration));
        }

        private static void Player_NewSongPlaying(string name, IMessageChannel commandChannel, DiscordPlayer dp)
        {
            commandChannel.SendMessageAsync(embed: embedString("Now playing",name, dp.currentStreamInfo.Bitrate.KiloBitsPerSecond, dp.currentVideoInfo.Author.ChannelTitle, (TimeSpan)dp.currentVideoInfo.Duration));
        }

        private static Embed embedString(string action,string song,double bitrate, string author, TimeSpan duration)
        {
            var botClient = Program.GetBot();
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = action,
                Description = $"{song}"
            }
            .WithColor(Discord.Color.Blue)
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
            if (serverPlayersMap.ContainsKey(channel.GuildId))
            {
                var usersInVoiceChannel = await serverPlayersMap.GetValueOrDefault(channel.GuildId).AudioChannel.GetUserAsync(sender.Id);
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
            if (serverPlayersMap.ContainsKey(channel.GuildId))
            {
                var usersInVoiceChannel = await serverPlayersMap.GetValueOrDefault(channel.GuildId).AudioChannel.GetUserAsync(sender.Id);
                if (usersInVoiceChannel == null)
                    return false;
                return true;
            }
            throw new DiscordPlayerNotConnectedException();
        }

        public static async Task<DiscordPlayer> GetServerDiscordPlayer(ulong guildId) => serverPlayersMap.GetValueOrDefault(guildId);
    }
}
