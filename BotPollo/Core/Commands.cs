using BotPollo.Attributes;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Core
{
    class Commands
    {
        static List<string> Songs = new List<string>();
        static DiscordPlayer player;

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
                    }).WithColor(Color.Blue)
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

        [Command("canzonedelpollo")]
        public static async Task CanzonePollo(SocketMessage msg)
        {
            if(Songs.Count == 0)
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
            int songIndex;
            if (msg.Content.Split(' ').Length == 1)
            {
                Dialogue dial = new Dialogue(msg.Author, msg.Channel, 30000);

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
                dial.Close();
            }
            else
            {
                songIndex = Int32.Parse(msg.Content.Split(' ')[1]); //Gets the first argument of the command
            }
            if((msg.Author as IGuildUser).VoiceChannel != null && (msg.Channel as IGuildChannel).GuildId == (msg.Author as IGuildUser).VoiceChannel.GuildId)
            {
                IVoiceChannel voiceChannel = (msg.Author as IGuildUser).VoiceChannel;
                try
                {
                    if (Program.GetBot().GetGuild((msg.Channel as SocketGuildChannel).Guild.Id).CurrentUser.VoiceState == null || player == null)
                    {
                        var audioClient = await voiceChannel.ConnectAsync();
                        player = new DiscordPlayer(audioClient,msg.Channel as IMessageChannel);
                        player.NewSongPlaying += Player_NewSongPlaying; //SISTEMA EVENTI CHE NON TRIGGERANO
                        player.SongAdded += Player_SongAdded;
                    }
                    await player.AddSongAsync(@"C:\Users\Administrator\Music\" + Songs[songIndex]);
                }
                catch(Exception ex)
                {
                    Logging.Logger.Console_Log(ex.StackTrace, Logging.LogLevel.Error);
                    Logging.Logger.Console_Log(ex.Message, Logging.LogLevel.Error);
                }
            }
            else
            {
                await msg.Channel.SendMessageAsync("You're not in a valid voice channel");
            }
        }

        private static void Player_SongAdded(string name, IMessageChannel commandChannel)
        {
            commandChannel.SendMessageAsync(embed: embedString("Added to queue",name));
        }

        private static void Player_NewSongPlaying(string name, IMessageChannel commandChannel)
        {
            commandChannel.SendMessageAsync(embed: embedString("Now playing",name));
        }

        private static Embed embedString(string action,string song)
        {
            var botClient = Program.GetBot();
            EmbedBuilder embed = new EmbedBuilder
            {
                Title = action,
                Description = $"`{song}`"
            }
            .WithColor(Color.Blue)
            .WithTimestamp(DateTime.Now);
            return embed.Build();
        }
    }
}
