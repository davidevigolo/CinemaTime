using BotPollo.Core;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BotPollo
{
    [ApiController]
    [Route("[controller]")]
    public class PlayerController : ControllerBase
    {
        // GET api/values 
        [HttpGet]
        public IActionResult Get()
        {
            return NotFound();
        }

        // GET api/values/5 
        [HttpGet("{id}")]
        public IActionResult Get(ulong id)
        {
            if (Commands.serverPlayersMap.ContainsKey(id))
            {
                return Ok(Commands.serverPlayersMap[id]);
            }
            return NotFound("player not found");
        }

        [HttpPost("{id}/executeCommand")]
        public IActionResult Post(ulong id,[FromQuery]string value)
        {
            if (value == "skip")
            {
                var player = Commands.serverPlayersMap.GetValueOrDefault(id);
                if (player == null) return BadRequest("player not found");
                player.Skip();
                return Accepted("Song skipped");
            }
            return BadRequest("command not found");
        }

        public struct Command
        {
            public string commandName;
        }
    }
}