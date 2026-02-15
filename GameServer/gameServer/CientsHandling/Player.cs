using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

namespace gameServer.ClientsHandling
{
    internal class Player
    {
        private static int _nextId = 0;
        private int _disconnectNotified = 0;

        private readonly NetworkStream _stream;
        private readonly MessagePackStreamReader _streamReader;

        public event Action<Player>? Disconnected;

        public int Id { get; }
        public string Pseudo { get; set; } = "";
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public bool IsAlive { get; set; } = true;
        public bool IsReady { get; set; }

        public Player(TcpClient client)
        {
            Id = System.Threading.Interlocked.Increment(ref _nextId);
            _stream = client.GetStream();
            _streamReader = new MessagePackStreamReader(_stream);
            Console.WriteLine($"[Player] {Id} : Connected");
        }

        public async Task<T?> ReadMessageAsync<T>(CancellationToken cancellationToken) where T : class
        {
            var msgpack = await _streamReader.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (msgpack == null)
            {
                return null;
            }

            return MessagePackSerializer.Deserialize<T>(msgpack.Value, cancellationToken: cancellationToken);
        }

        public void SendMessage<T>(T message)
        {
            MessagePackSerializer.Serialize(_stream, message);
            _stream.Flush();
        }

        public void NotifyDisconnected()
        {
            if (Interlocked.Exchange(ref _disconnectNotified, 1) == 0)
            {
                Disconnected?.Invoke(this);
            }
        }
    }
}
