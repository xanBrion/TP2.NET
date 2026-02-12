using System;
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

        private readonly RoomManager _roomManager = new RoomManager();

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

                var firstMessage = await player.ReadMessageAsync<IClientMessage>(cancellationToken).ConfigureAwait(false);

                switch (firstMessage)
                {
                    case null:
                        Console.WriteLine($"[TCPServer] Player {player.Id} : Disconnected before first message.");
                        return;

                    case ObserverConnectRequest observeRequest:
                        await HandleObserverClientAsync(player, observeRequest, cancellationToken).ConfigureAwait(false);
                        return;

                    case ClientPseudoResponse pseudoResponse:
                        player.Pseudo = pseudoResponse.Pseudo;
                        Console.WriteLine($"[TCPServer] Player {player.Id} : Set pseudo to '{player.Pseudo}'");
                        await HandlePlayerClientAsync(player, cancellationToken).ConfigureAwait(false);
                        return;

                    default:
                        player.SendMessage<IServerMessage>(new ErrorResponse { Code = "invalid_first_message" });
                        return;
                }
            }
        }

        private async Task HandlePlayerClientAsync(Player player, CancellationToken cancellationToken)
        {
            Room? assignedRoom = null;
            try
            {
                assignedRoom = await HandleLobbySelectionAsync(player, cancellationToken).ConfigureAwait(false);
                if (assignedRoom == null)
                {
                    Console.WriteLine($"[TCPServer] Player {player.Id} : Disconnected before lobby selection.");
                    return;
                }

                player.Disconnected += _ => HandlePlayerDisconnected(player, assignedRoom);
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
            }
            finally
            {
                if (assignedRoom != null)
                {
                    player.NotifyDisconnected();
                }
            }
        }

        private async Task HandleObserverClientAsync(
            Player player,
            ObserverConnectRequest observeRequest,
            CancellationToken cancellationToken)
        {
            var observer = new Observer(player);
            Room? observedRoom = null;
            try
            {
                if (!_roomManager.TryAttachObserver(observeRequest.RoomId, observer, out observedRoom))
                {
                    observer.SendMessage<IServerMessage>(new ErrorResponse { Code = "room_not_found" });
                    return;
                }

                observer.Disconnected += _ => HandleObserverDisconnected(observer, observedRoom);
                observer.SendMessage<IServerMessage>(new ObserverJoined { RoomId = observedRoom.Id });
                observer.SendMessage<IServerMessage>(
                    observedRoom.BuildWorldStateSnapshot(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                Console.WriteLine($"[TCPServer] Observer {observer.Id} : Watching room {observedRoom.Id}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = await observer.ReadMessageAsync<IClientMessage>(cancellationToken).ConfigureAwait(false);
                    if (message == null)
                    {
                        break;
                    }

                    observer.SendMessage<IServerMessage>(new ErrorResponse { Code = "observer_readonly" });
                }
            }
            finally
            {
                if (observedRoom != null)
                {
                    observer.NotifyDisconnected();
                }
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
                            var room = _roomManager.QuickJoin(player);
                            player.SendMessage<IServerMessage>(new LobbyJoined { RoomId = room.Id });
                            return room;
                        }
                    case LobbyListRequest:
                        {
                            var lobbyList = _roomManager.GetLobbyList();
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

                            if (_roomManager.TryJoinRoom(roomId, player, out var room))
                            {
                                player.SendMessage<IServerMessage>(new LobbyJoined { RoomId = room.Id });
                                return room;
                            }

                            player.SendMessage<IServerMessage>(new ErrorResponse { Code = "room_not_found_or_full" });
                            break;
                        }
                    case LobbyCreateRequest:
                        {
                            var room = _roomManager.CreateRoomAndJoin(player);
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

        private void HandlePlayerDisconnected(Player player, Room room)
        {
            _roomManager.HandlePlayerDisconnected(player, room);
        }

        private void HandleObserverDisconnected(Observer observer, Room room)
        {
            _roomManager.HandleObserverDisconnected(observer, room);
        }
    }
}
