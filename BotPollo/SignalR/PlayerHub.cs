using BotPollo.Attributes;
using BotPollo.Core;
using Microsoft.AspNetCore.SignalR;

namespace BotPollo.SignalR
{
    /*
     * Client -> RPC -> Server:
     * Play/Add song
     * Skip
     * Seek
     * Pause/Resume
     * SetEQ
     * 
     * Server -> RPC -> Client:
     * Queue changed updates
     * Bot status updates (disconnected/connected)
     * 
     * Unique PlayerStatusUpdate with this infos:
     * Play/Pause
     * Timestamp of the stream position
     * Song that is being played
     * 
     * 
     */
    public class PlayerHub : Hub
    {
        struct UserConnectionState{
            public ulong Id;
            public string Token;
            public string? GroupName;
            public bool HasSubscribed; //Avoid connections that don't actually make use of the apis
        }
        private List<UserConnectionState> _states = new List<UserConnectionState>();
        public override async Task OnConnectedAsync()
        {
            await Clients.All.SendAsync("ReceiveMessage", $"{Context.ConnectionId} has joined");
            await Groups.AddToGroupAsync($"{Context.ConnectionId}", "208664475623096321");
            /*var state = new UserConnectionState
            {
                Id = 0,
                Token = "Development mode",
                HasSubscribed = false,
            };
            _states.Add(state);*/
        }
        public override Task OnDisconnectedAsync(Exception? exception)
        {
            return base.OnDisconnectedAsync(exception);
        }
        public async Task JoinGroup(string groupdId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupdId.ToString());
        }
        public void SendMessage(string message)
        {
            Clients.All.SendAsync("ReceiveMessage", message);
        }
        public async Task<bool> PlaySong(string query, string playerId)
        {
            IDiscordPlayer player = Globals.serverPlayersMap[UInt64.Parse(playerId)];
            return await player.AddSongAsync(query);
        }
        public async Task<bool> SkipSong(ulong playerId)
        {
            IDiscordPlayer player = Globals.serverPlayersMap[playerId];
            return player.Skip();
        }
    }
}
