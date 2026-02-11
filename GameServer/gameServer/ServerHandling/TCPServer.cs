using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using gameServer.ClientsHandling;

namespace gameServer.ServerHandling
{
    internal class TCPServer
    {
        private TcpListener _tcpListener;


        private readonly Dictionary<int, Room> _roomsById = new Dictionary<int, Room>();
        private int _nextRoomId = 1;

        public TCPServer()
        {

        }

        public void Start(CancellationToken cancellationToken = default)
        {
            StartServerAsync(cancellationToken).GetAwaiter().GetResult();
        }

        private async Task StartServerAsync(CancellationToken cancellationToken)
        {
            var port = 13000;
            var hostAddress = IPAddress.Parse("0.0.0.0");
            _tcpListener = new TcpListener(hostAddress, port);
            _tcpListener.Start();

            using TcpClient client = _tcpListener.AcceptTcpClient();
            var player = new Player(client);
            
            Console.WriteLine($"[TCPServer] Player {player.Id} : Connected");
            await player.InitializeAsync(cancellationToken).ConfigureAwait(false);

            Room? assignedRoom = null;

            foreach (Room room in _roomsById.Values)
            {
                Console.WriteLine($"[TCPServer] Player {player.Id} : Checking room {room.Id}");

                if (room.IsFull)
                {
                    continue;
                }
                else
                {
                    room.AddPlayer(player);
                    assignedRoom = room;
                    break;
                }

            }
            if (assignedRoom == null)
            {

                Console.WriteLine($"[TCPServer] Player {player.Id} : No available room, creating new one.");
                var newRoom = new Room(_nextRoomId++);
                newRoom.AddPlayer(player);
                _roomsById.Add(newRoom.Id, newRoom);
                assignedRoom = newRoom;

                Console.WriteLine($"[TCPServer] Player {player.Id} : Room {newRoom.Id} created and player added.");


            }


            while (!cancellationToken.IsCancellationRequested)
            {

                var receivedMessage = await player.ReadMessageAsync<NetworkMessage>(cancellationToken).ConfigureAwait(false);
                if (receivedMessage == null)
                {
                    break;
                }

                Console.WriteLine($"[TCPServer] Player {player.Id} : Received {receivedMessage.Type} / {receivedMessage.Payload}");
                assignedRoom?.HandleMessage(player, receivedMessage);

            }

        }
    }

    [MessagePackObject]
    internal sealed class NetworkMessage
    {
        [Key(0)]
        public string Type { get; set; } = "";

        [Key(1)]
        public string Payload { get; set; } = "";
    }
}
