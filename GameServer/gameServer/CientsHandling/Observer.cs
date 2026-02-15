using System;
using System.Threading;
using System.Threading.Tasks;

namespace gameServer.ClientsHandling
{
    internal sealed class Observer
    {
        private readonly Player _client;

        public Observer(Player client)
        {
            _client = client;
        }

        public int Id => _client.Id;

        public string Pseudo => _client.Pseudo;

        public event Action<Player>? Disconnected
        {
            add => _client.Disconnected += value;
            remove => _client.Disconnected -= value;
        }

        public Task<T?> ReadMessageAsync<T>(CancellationToken cancellationToken) where T : class
        {
            return _client.ReadMessageAsync<T>(cancellationToken);
        }

        public void SendMessage<T>(T message)
        {
            _client.SendMessage(message);
        }

        public void NotifyDisconnected()
        {
            _client.NotifyDisconnected();
        }
    }
}
