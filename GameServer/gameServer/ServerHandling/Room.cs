using System.Collections.Generic;
using System.Linq;
using gameServer.ClientsHandling;
using gameServer.GameHandling;

namespace gameServer.ServerHandling
{
    internal class Room
    {
        private const int MinPlayersToStart = 2;
        private const int MaxPlayers = 10;
        private readonly Dictionary<int, Player> _playersById = new Dictionary<int, Player>();
        private readonly Dictionary<int, Observer> _observersById = new Dictionary<int, Observer>();
        private readonly object _playersLock = new object();
        private string _lastKnownState = "empty";
        private Game? _game;

        public int Id { get; }

        public Room(int id)
        {
            Id = id;
        }

        public int PlayerCount
        {
            get
            {
                lock (_playersLock)
                {
                    return _playersById.Count;
                }
            }
        }

        public int Capacity => MaxPlayers;

        public int ObserverCount
        {
            get
            {
                lock (_playersLock)
                {
                    return _observersById.Count;
                }
            }
        }

        public int ParticipantCount
        {
            get
            {
                lock (_playersLock)
                {
                    return _playersById.Count + _observersById.Count;
                }
            }
        }

        public bool IsFull
        {
            get
            {
                lock (_playersLock)
                {
                    return _playersById.Count >= MaxPlayers;
                }
            }
        }

        public bool AddPlayer(Player player)
        {
            lock (_playersLock)
            {
                if (_playersById.Count >= MaxPlayers || _playersById.ContainsKey(player.Id))
                {
                    return false;
                }

                player.Ready = false;
                _playersById.Add(player.Id, player);
                return true;
            }
        }

        public void RemovePlayer(int playerId)
        {
            lock (_playersLock)
            {
                _playersById.Remove(playerId);
            }
        }

        public bool AddObserver(Observer observer)
        {
            lock (_playersLock)
            {
                if (_observersById.ContainsKey(observer.Id))
                {
                    return false;
                }

                _observersById.Add(observer.Id, observer);
                return true;
            }
        }

        public void RemoveObserver(int observerId)
        {
            lock (_playersLock)
            {
                _observersById.Remove(observerId);
            }
        }

        public GameStateSnapshot BuildSnapshot()
        {
            lock (_playersLock)
            {
                return new GameStateSnapshot
                {
                    RoomId = Id,
                    State = _lastKnownState
                };
            }
        }

        public void HandleMessage(Player player, IClientMessage message)
        {
            // Handle player ready/unready updates.
            if (message is PlayerReadyUpdate readyUpdate)
            {
                List<Player> players;
                List<Observer> observers;
                bool shouldStart;

                // Update ready state and evaluate game start under lock.
                lock (_playersLock)
                {
                    // Ignore messages from players not in this room anymore.
                    if (!_playersById.ContainsKey(player.Id))
                    {
                        return;
                    }

                    // Update sender ready flag and snapshot current participants.
                    player.Ready = readyUpdate.Ready;
                    players = _playersById.Values.ToList();
                    observers = _observersById.Values.ToList();
                    // Start only once, with enough players, and everyone ready.
                    shouldStart = _game == null
                        && players.Count >= MinPlayersToStart
                        && players.All(p => p.Ready);

                    if (shouldStart)
                    {
                        _game = new Game(Id);
                        _lastKnownState = "started";
                    }
                }

                // Notify players/observers about the sender ready state change.
                var readyChanged = new PlayerReadyChanged
                {
                    RoomId = Id,
                    PlayerId = player.Id,
                    Ready = player.Ready
                };

                foreach (var roomPlayer in players)
                {
                    roomPlayer.SendMessage<IServerMessage>(readyChanged);
                }

                foreach (var observer in observers)
                {
                    observer.SendMessage<IServerMessage>(readyChanged);
                }

                // If game just started, broadcast the start event + snapshot.
                if (shouldStart)
                {
                    System.Console.WriteLine($"[Room] {Id} : Game started ({players.Count} players ready)");
                    var gameStarted = new GameStarted { RoomId = Id };
                    var snapshot = new GameStateSnapshot
                    {
                        RoomId = Id,
                        State = _lastKnownState
                    };

                    foreach (var roomPlayer in players)
                    {
                        roomPlayer.SendMessage<IServerMessage>(gameStarted);
                        roomPlayer.SendMessage<IServerMessage>(snapshot);
                    }

                    foreach (var observer in observers)
                    {
                        observer.SendMessage<IServerMessage>(gameStarted);
                        observer.SendMessage<IServerMessage>(snapshot);
                    }
                }

                return;
            }

            // Handle player moves by updating room state and notifying observers.
            if (message is PlayerDisplacement move)
            {
                List<Observer> observers;
                // Update state under lock and copy observer list for sending outside lock.
                lock (_playersLock)
                {
                    _lastKnownState = move.Payload;
                    observers = _observersById.Values.ToList();
                }

                var snapshot = new GameStateSnapshot
                {
                    RoomId = Id,
                    State = _lastKnownState
                };

                foreach (var observer in observers)
                {
                    observer.SendMessage<IServerMessage>(snapshot);
                }
            }
        }
    }
}
