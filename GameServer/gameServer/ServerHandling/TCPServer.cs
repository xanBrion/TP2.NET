using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using gameServer.ClientsHandling;

namespace gameServer.ServerHandling
{
    internal class TCPServer
    {
        private TcpListener _tcpListener;

        private readonly Dictionary<int, Room> _roomsById = new Dictionary<int, Room>();
        private readonly object _roomsLock = new object();
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

            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = await _tcpListener.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            using (client)
            {
                var player = new Player(client);
                Console.WriteLine($"[TCPServer] Player {player.Id} : Connected");
                await player.InitializeAsync(cancellationToken).ConfigureAwait(false);

                var assignedRoom = await HandleLobbySelectionAsync(player, cancellationToken).ConfigureAwait(false);
                if (assignedRoom == null)
                {
                    Console.WriteLine($"[TCPServer] Player {player.Id} : Disconnected before lobby selection.");
                    return;
                }

                Console.WriteLine($"[TCPServer] Player {player.Id} : Joined room {assignedRoom.Id}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var receivedMessage = await player.ReadMessageAsync<IClientMessage>(cancellationToken).ConfigureAwait(false);
                    if (receivedMessage == null)
                    {
                        break;
                    }

                    Console.WriteLine($"[TCPServer] Player {player.Id} : Received {receivedMessage.GetType().Name}");
                    assignedRoom.HandleMessage(player, receivedMessage);
                }

                assignedRoom.RemovePlayer(player.Id);
                Console.WriteLine($"[TCPServer] Player {player.Id} : Disconnected from room {assignedRoom.Id}");
            }
        }

        private async Task<Room?> HandleLobbySelectionAsync(Player player, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await player.ReadMessageAsync<IClientMessage>(cancellationToken).ConfigureAwait(false);
                if (message == null)
                {
                    return null;
                }

                switch (message)
                {
                    case QuickJoinRequest:
                    {
                        var room = QuickJoin(player);
                        player.SendMessage<IServerMessage>(new LobbyJoined { RoomId = room.Id });
                        return room;
                    }
                    case LobbyListRequest:
                    {
                        var lobbyList = GetLobbyList();
                        player.SendMessage<IServerMessage>(new LobbyListResponse { Lobbies = lobbyList });
                        break;
                    }
                    case LobbyJoinRequest joinRequest:
                    {
                        var roomId = joinRequest.RoomId;
                        if (roomId <= 0)
                        {
                            player.SendMessage<IServerMessage>(new ErrorResponse { Code = "invalid_room_id" });
                            break;
                        }

                        if (TryJoinRoom(roomId, player, out var room))
                        {
                            player.SendMessage<IServerMessage>(new LobbyJoined { RoomId = room.Id });
                            return room;
                        }

                        player.SendMessage<IServerMessage>(new ErrorResponse { Code = "room_not_found_or_full" });
                        break;
                    }
                    case LobbyCreateRequest:
                    {
                        var room = CreateRoomAndJoin(player);
                        player.SendMessage<IServerMessage>(new LobbyJoined { RoomId = room.Id });
                        return room;
                    }
                    default:
                    {
                        player.SendMessage<IServerMessage>(new ErrorResponse { Code = "unknown_lobby_action" });
                        break;
                    }
                }
            }

            return null;
        }

        private Room QuickJoin(Player player)
        {
            lock (_roomsLock)
            {
                foreach (var room in _roomsById.Values)
                {
                    if (room.IsFull)
                    {
                        continue;
                    }

                    room.AddPlayer(player);
                    return room;
                }

                var newRoom = new Room(_nextRoomId++);
                newRoom.AddPlayer(player);
                _roomsById.Add(newRoom.Id, newRoom);
                return newRoom;
            }
        }

        private bool TryJoinRoom(int roomId, Player player, out Room room)
        {
            lock (_roomsLock)
            {
                if (_roomsById.TryGetValue(roomId, out room) && !room.IsFull)
                {
                    room.AddPlayer(player);
                    return true;
                }
            }

            room = null!;
            return false;
        }

        private Room CreateRoomAndJoin(Player player)
        {
            lock (_roomsLock)
            {
                var newRoom = new Room(_nextRoomId++);
                newRoom.AddPlayer(player);
                _roomsById.Add(newRoom.Id, newRoom);
                return newRoom;
            }
        }

        private List<LobbyInfo> GetLobbyList()
        {
            var list = new List<LobbyInfo>();
            lock (_roomsLock)
            {
                foreach (var room in _roomsById.Values)
                {
                    list.Add(new LobbyInfo
                    {
                        Id = room.Id,
                        PlayerCount = room.PlayerCount,
                        Capacity = room.Capacity
                    });
                }
            }

            return list;
        }
    }
}
