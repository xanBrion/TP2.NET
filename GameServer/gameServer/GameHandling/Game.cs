namespace gameServer.GameHandling
{
    internal sealed class Game
    {
        public Game(int roomId)
        {
            RoomId = roomId;
            StartedAtUtc = System.DateTime.UtcNow;
        }

        public int RoomId { get; }

        public System.DateTime StartedAtUtc { get; }
    }
}
