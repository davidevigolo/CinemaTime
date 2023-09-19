using BotPollo.Attributes;
using BotPollo.Core;
using BotPollo.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UDPStatusServer.Messages;

namespace BotPollo.UDP
{
    public interface IConnectionManager
    {
        //Dictionary<ulong, IDiscordPlayer> Players { get; set; }
        void Start();
    }

    public class ConnectionManager : IConnectionManager
    {

        private UdpClient listener { get; set; }
        private IPEndPoint[] EndPoints { get; set; }
        private Dictionary<ulong, IDiscordPlayer> Players;
        private ILogger<ConnectionManager> _logger;
        public ConnectionManager(ILogger<ConnectionManager> logger)
        {
            EndPoints = new IPEndPoint[10]; //max 10 connections for testing purposes
            Players = Globals.serverPlayersMap;
        }

        public void Start()
        {
            listener = new UdpClient(32980);
            Thread listeningThread = new Thread(Receive);
            listeningThread.Start();
        }

        public void Receive()
        {
            IPEndPoint iPEndPoint = new IPEndPoint(IPAddress.Any, 32980);
            byte[] buffer = new byte[4096];
            while (true)
            {
                try
                {
                    buffer = listener.Receive(ref iPEndPoint);
                    string message = Encoding.UTF8.GetString(buffer);
                    DispatchCommand(message, iPEndPoint);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"There was a connection error with: {iPEndPoint.Address}:{iPEndPoint.Port}", iPEndPoint);
                }
            }
        }

        public void DispatchCommand(string message, IPEndPoint iPEndPoint)
        {
            Thread executionThread = new Thread(() => ExecuteCommand(message, iPEndPoint));
            executionThread.Start();
        }

        private async void ExecuteCommand(string jsonCommand, IPEndPoint remoteEndPoint)
        {

            var command = JsonConvert.DeserializeObject<UDPStatusServer.Messages.Message>(jsonCommand);

            //logica comandi

            switch (command.OpCode)
            {
                case 101:
                    var guildId = JsonConvert.DeserializeObject<GetPlayerInfoParams>(command.Params).GuildId;
                    try
                    {
                        var player = Players[guildId];
                        var users = await player.AudioChannel.GetUsersAsync().ToListAsync();
                        var queue = player.SongQueue.ToArray();
                        var response = new
                        {
                            CurrentSong = player.CurrentQueueObject.VideoInfo,
                            Queue = player.SongQueue.ToArray(),
                            AudioChannelBitrate = player.AudioChannel.Bitrate,
                            AudioChannelId = player.AudioChannel.Id,
                            Status = player.isPlaying ? 1 : 0
                        };
                        var jsonData = JsonConvert.SerializeObject(response);
                        var data = Encoding.UTF8.GetBytes(jsonData);
                        listener.Send(data, data.Length, remoteEndPoint);
                    }
                    catch (Exception ex)
                    {
                        var buffer = Encoding.UTF8.GetBytes("{ Status: \"Error\" }");
                        listener.Send(buffer, buffer.Length, remoteEndPoint);
                    }
                    break;
                case 201:
                    var guildId2 = JsonConvert.DeserializeObject<GetPlayerInfoParams>(command.Params).GuildId;
                    var player2 = Players[guildId2];

                    break;
            }
        }
    }
}