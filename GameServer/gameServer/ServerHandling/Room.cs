using System.Collections.Generic;
using gameServer.ClientsHandling;

namespace gameServer.ServerHandling
{
    internal class Room
    {
        private const int MaxPlayers = 10;
        private readonly Dictionary<int, Player> _playersById = new Dictionary<int, Player>();
        private readonly object _playersLock = new object();

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

        public void StartGame()
        {
            // TODO: initialize game state
        }

        public void HandleMessage(Player player, IClientMessage message)
        {
            // TODO: route message to game logic
        }
    }
}
