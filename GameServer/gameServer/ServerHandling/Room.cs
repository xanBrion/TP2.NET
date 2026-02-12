using System.Collections.Generic;
using gameServer.ClientsHandling;

namespace gameServer.ServerHandling
{
    internal class Room
    {
        private const int MaxPlayers = 10;
        private readonly Dictionary<int, Player> _playersById = new Dictionary<int, Player>();
        private readonly Dictionary<int, Observer> _observersById = new Dictionary<int, Observer>();
        private readonly object _playersLock = new object();
        private string _lastKnownState = "empty";

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

        public void StartGame()
        {
            // TODO: initialize game state
        }

        public void HandleMessage(Player player, IClientMessage message)
        {
            if (message is PlayerDisplacement move)
            {
                lock (_playersLock)
                {
                    _lastKnownState = move.Payload;
                    var snapshot = new GameStateSnapshot
                    {
                        RoomId = Id,
                        State = _lastKnownState
                    };

                    foreach (var observer in _observersById.Values)
                    {
                        observer.SendMessage<IServerMessage>(snapshot);
                    }
                }
            }
        }
    }
}
