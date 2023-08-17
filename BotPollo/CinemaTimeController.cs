using BotPollo.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static BotPollo.Core.DiscordPlayer;

namespace BotPollo
{
    public class CinemaTimeController
    {
        public const int MMF_MAX_SIZE = 4092;
        public const int MMF_VIEW_SIZE = 4092;

        public string Get()
        {
            return "helo";
            /*string result = Commands.serverPlayersMap.GetValueOrDefault(id).currentVideoInfo.Title;
            if (result == null)
            {
                return Content(System.Net.HttpStatusCode.NotFound,"either server id is invalid or bot isn't connected");
            }
            return Content(System.Net.HttpStatusCode.OK,result);*/

        }

        private async Task<string> GetMMFProperty(ulong serverId,string propertyName)
        {
            MemoryMappedFile MMF = MemoryMappedFile.OpenExisting(serverId.ToString());
            MemoryMappedViewStream mmfStream = MMF.CreateViewStream(0, MMF_MAX_SIZE, MemoryMappedFileAccess.ReadWrite);
            byte[] buffer = new byte[MMF_MAX_SIZE];
            await mmfStream.ReadAsync(buffer);
            string bytes = Encoding.UTF8.GetString(buffer);
            JObject data = JsonConvert.DeserializeObject<JObject>(bytes);
            return data[propertyName].ToString();
        }
    }
}
