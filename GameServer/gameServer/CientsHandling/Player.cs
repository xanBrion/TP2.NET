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

        private readonly MessagePackStreamReader _streamReader;

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
            Console.WriteLine($"[Player] {Id} : Connected");
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

        private async Task InterrogateClientForInfoAsync(CancellationToken cancellationToken)
        {
            SendMessage(new NetworkMessage { Type = "request", Payload = "Pseudo" });
            var responsePseudo = await ReadMessageAsync<NetworkMessage>(cancellationToken).ConfigureAwait(false);
            if (responsePseudo != null)
            {
                Pseudo = responsePseudo.Payload;
            }

            
        }
    }
}
