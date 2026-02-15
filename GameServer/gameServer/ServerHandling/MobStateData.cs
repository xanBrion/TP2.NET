using MessagePack;

namespace gameServer.ServerHandling
{
    [MessagePackObject]
    internal sealed class MobStateData
    {
        [Key(0)]
        public int MobId { get; set; }

        [Key(1)]
        public float X { get; set; }

        [Key(2)]
        public float Y { get; set; }

        [Key(3)]
        public float Speed { get; set; }

        [Key(4)]
        public float Angle { get; set; }

        [Key(5)]
        public float VelocityX { get; set; }

        [Key(6)]
        public float VelocityY { get; set; }
    }
}
