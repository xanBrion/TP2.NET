using System.Collections.Generic;
using gameServer.ClientsHandling;

namespace gameServer.ServerHandling
{
    internal class Room
    {
        private const int MaxPlayers = 10;
        private readonly Dictionary<int, Player> _playersById = new Dictionary<int, Player>();

        public int Id { get; }

        public Room(int id)
        {
            Id = id;
        }

        public bool IsFull => _playersById.Count >= MaxPlayers;

        public bool AddPlayer(Player player)
        {
            if (IsFull || _playersById.ContainsKey(player.Id))
            {
                return false;
            }

            _playersById.Add(player.Id, player);
            return true;
        }

        public void RemovePlayer(int playerId)
        {
            _playersById.Remove(playerId);
        }

        public void StartGame()
        {
            // TODO: initialize game state
        }

        public void HandleMessage(Player player, NetworkMessage message)
        {
            // TODO: route message to game logic
        }
    }
}
