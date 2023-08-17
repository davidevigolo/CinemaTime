using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UDPStatusServer.Messages;
using BotPollo.Core;
using System.Runtime.Intrinsics.Arm;

namespace UDPStatusServer
{
    public class ConnectionManager
    {

        UdpClient listener { get; set; }
        IPEndPoint[] EndPoints { get; set; }
        public ConnectionManager(DiscordPlayer[] serverPlayersMap) {
            EndPoints = new IPEndPoint[10]; //max 10 connections for testing purposes
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
                buffer = listener.Receive(ref iPEndPoint);
                string message = Encoding.UTF8.GetString(buffer);
                DispatchCommand(message, iPEndPoint);
            }
        }

        public void DispatchCommand(string message, IPEndPoint iPEndPoint)
        {
            Thread executionThread = new Thread(() => ExecuteCommand(message, iPEndPoint));
            executionThread.Start();
        }

        private void ExecuteCommand(string jsonCommand, IPEndPoint remoteEndPoint)
        {

            Console.WriteLine(jsonCommand);

            var command = JsonConvert.DeserializeObject<Message>(jsonCommand);

            //logica comandi

            switch (command.OpCode)
            {
                case 101:
                    var guildId = command.Params.First();
                    var player = Commands.serverPlayersMap[guildId];
                    var response = new
                    {
                        CurrentSong = player.currentVideoInfo,
                        Queue = player.SongQueue.ToArray(),
                    };
                    UdpClient responseClient = new UdpClient(remoteEndPoint);
                    responseClient.Send(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response)));
                    break;
            }
        }
    }
}