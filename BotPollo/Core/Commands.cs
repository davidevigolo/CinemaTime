using BotPollo.Attributes;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Core
{
    class Commands
    {
        [Command("checkpunti")]
        public static async Task CheckPunti(SocketMessage msg)
        {
            var channel = msg.Channel;
            await channel.SendMessageAsync("troppi troppi");
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
            Dialogue dial = new Dialogue(msg.Author, msg.Channel, 5000);
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
                    await dial.ReplyAsync("Attendi...", true, true);
                    await dial.ReplyAsync($"Titolo: {result} Descrizione: {resp}", false, false, true);
                    dial.Close();
                    await firstMessage.DeleteAsync();
                }
            }
        }
    }
}
