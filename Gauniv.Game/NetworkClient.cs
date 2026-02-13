using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

public sealed class GameServerNetworkClient : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

    private TcpClient? _client;
    private NetworkStream? _stream;
    private MessagePackStreamReader? _reader;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;

    public event Action<IServerMessage>? ServerMessageReceived;
    public event Action<string>? ErrorOccurred;

    public bool IsConnected => _client?.Connected == true;

    public string Pseudo { get; private set; } = string.Empty;

    public async Task ConnectAsPlayerAsync(
        string host,
        int port,
        string pseudo,
        CancellationToken cancellationToken = default)
    {
        if (IsConnected)
        {
            return;
        }

        _client = new TcpClient();
        await _client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);

        _stream = _client.GetStream();
        _reader = new MessagePackStreamReader(_stream);

        Pseudo = pseudo;
        _readLoopCts = new CancellationTokenSource();
        _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token));

        await SendAsync(new ClientPseudoResponse { Pseudo = pseudo }, cancellationToken).ConfigureAwait(false);
    }

    public Task RequestLobbyListAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync(new LobbyListRequest(), cancellationToken);
    }

    public Task QuickJoinAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync(new QuickJoinRequest(), cancellationToken);
    }

    public Task JoinLobbyAsync(int roomId, CancellationToken cancellationToken = default)
    {
        return SendAsync(new LobbyJoinRequest { RoomId = roomId }, cancellationToken);
    }

    public Task SetReadyAsync(bool isReady, CancellationToken cancellationToken = default)
    {
        return SendAsync(new PlayerReadyUpdate { IsReady = isReady }, cancellationToken);
    }

    public Task SendPositionAsync(float x, float y, CancellationToken cancellationToken = default)
    {
        return SendAsync(new PlayerPositionUpdate
        {
            X = x,
            Y = y,
            ClientTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }, cancellationToken);
    }

    private async Task SendAsync(IClientMessage message, CancellationToken cancellationToken)
    {
        if (_stream == null)
        {
            throw new InvalidOperationException("Not connected to game server.");
        }

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            MessagePackSerializer.Serialize(_stream, message);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_reader == null)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var packet = await _reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (packet == null)
                {
                    return;
                }

                var message = MessagePackSerializer.Deserialize<IServerMessage>(
                    packet.Value,
                    cancellationToken: cancellationToken);
                ServerMessageReceived?.Invoke(message);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
    }

    public void Dispose()
    {
        try
        {
            _readLoopCts?.Cancel();
        }
        catch
        {
        }

        _readLoopCts?.Dispose();
        _stream?.Dispose();
        _client?.Dispose();
        _sendLock.Dispose();
    }
}
