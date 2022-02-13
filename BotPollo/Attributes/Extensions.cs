using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo.Attributes
{
    static internal class Extensions
    {
        public static async Task DeleteSafeAsync(this IMessage msg,ICollection<IMessage> collection)
        {
            collection.Remove(msg);
            await msg.DeleteAsync();
        }
    }
}
