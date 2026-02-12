using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using gameServer.ServerHandling;
using MessagePack;

namespace gameServer.ClientsHandling
{
    internal class Player
    {
        private static int _nextId = 0;
        private int _disconnectNotified = 0;

        private readonly MessagePackStreamReader _streamReader;

        public event Action<Player>? Disconnected;

        public int Id { get; }
        public string Pseudo { get; set; } = "";
        public TcpClient Client { get; }
        public NetworkStream Stream { get; }

        public Player(TcpClient client)
        {
            Id = System.Threading.Interlocked.Increment(ref _nextId);
            Client = client;
            Stream = client.GetStream();
            _streamReader = new MessagePackStreamReader(Stream);
            // InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
            Console.WriteLine($"[Player] {Id} : {Pseudo} Connected");
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await InterrogateClientForInfoAsync(cancellationToken).ConfigureAwait(false);
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
            MessagePackSerializer.Serialize(Stream, message);
            Stream.Flush();
        }

        public void NotifyDisconnected()
        {
            if (Interlocked.Exchange(ref _disconnectNotified, 1) == 0)
            {
                Disconnected?.Invoke(this);
            }
        }

        private async Task InterrogateClientForInfoAsync(CancellationToken cancellationToken)
        {
            SendMessage<IServerMessage>(new ServerPseudoRequest());

            while (!cancellationToken.IsCancellationRequested)
            {
                var response = await ReadMessageAsync<IClientMessage>(cancellationToken).ConfigureAwait(false);
                if (response is ClientPseudoResponse pseudoResponse)
                {
                    Pseudo = pseudoResponse.Pseudo;
                    return;
                }
            }
        }
    }
}
