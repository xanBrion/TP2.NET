using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

using var client = new TcpClient("127.0.0.1", 13000);
using var stream = client.GetStream();
using var reader = new MessagePackStreamReader(stream);

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

await HandlePseudoHandshakeAsync(reader, stream, cts.Token).ConfigureAwait(false);

var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "auto";
switch (mode)
{
    case "quickjoin":
        SendClientMessage(stream, new QuickJoinRequest());
        if (!await WaitForJoinAsync(reader, cts.Token).ConfigureAwait(false))
        {
            return;
        }
        break;
    case "list":
        SendClientMessage(stream, new LobbyListRequest());
        await PrintLobbyListAsync(reader, cts.Token).ConfigureAwait(false);
        return;
    case "join":
        if (args.Length < 2 || !int.TryParse(args[1], out var roomId))
        {
            Console.WriteLine("Usage: TestClient join <roomId>");
            return;
        }
        SendClientMessage(stream, new LobbyJoinRequest { RoomId = roomId });
        if (!await WaitForJoinAsync(reader, cts.Token).ConfigureAwait(false))
        {
            return;
        }
        break;
    case "create":
        SendClientMessage(stream, new LobbyCreateRequest());
        if (!await WaitForJoinAsync(reader, cts.Token).ConfigureAwait(false))
        {
            return;
        }
        break;
    default:
        await AutoJoinAsync(reader, stream, cts.Token).ConfigureAwait(false);
        break;
}

SendClientMessage(stream, new PlacementPionMessage { Payload = "hello" });
Console.WriteLine("Sent");

static async Task HandlePseudoHandshakeAsync(
    MessagePackStreamReader reader,
    NetworkStream stream,
    CancellationToken cancellationToken)
{
    while (true)
    {
        var request = await ReadMessageAsync<IServerMessage>(reader, cancellationToken).ConfigureAwait(false);
        if (request == null)
        {
            return;
        }

        if (request is ServerPseudoRequest)
        {
            SendClientMessage(stream, new ClientPseudoResponse { Pseudo = "TestPlayer" });
            return;
        }
    }
}

static async Task AutoJoinAsync(
    MessagePackStreamReader reader,
    NetworkStream stream,
    CancellationToken cancellationToken)
{
    SendClientMessage(stream, new LobbyListRequest());
    var lobbyListMessage = await ReadMessageAsync<IServerMessage>(reader, cancellationToken).ConfigureAwait(false);
    if (lobbyListMessage is not LobbyListResponse lobbyList)
    {
        Console.WriteLine("No lobby list response.");
        return;
    }

    LobbyInfo? targetLobby = null;
    foreach (var lobby in lobbyList.Lobbies)
    {
        if (lobby.PlayerCount < lobby.Capacity)
        {
            targetLobby = lobby;
            break;
        }
    }

    if (targetLobby != null)
    {
        SendClientMessage(stream, new LobbyJoinRequest { RoomId = targetLobby.Id });
    }
    else
    {
        SendClientMessage(stream, new LobbyCreateRequest());
    }

    await WaitForJoinAsync(reader, cancellationToken).ConfigureAwait(false);
}

static async Task PrintLobbyListAsync(
    MessagePackStreamReader reader,
    CancellationToken cancellationToken)
{
    var lobbyListMessage = await ReadMessageAsync<IServerMessage>(reader, cancellationToken).ConfigureAwait(false);
    if (lobbyListMessage is not LobbyListResponse lobbyList)
    {
        Console.WriteLine("No lobby list response.");
        return;
    }

    if (lobbyList.Lobbies.Count == 0)
    {
        Console.WriteLine("No lobbies.");
        return;
    }

    foreach (var lobby in lobbyList.Lobbies)
    {
        Console.WriteLine($"Lobby {lobby.Id} : {lobby.PlayerCount}/{lobby.Capacity}");
    }
}

static async Task<bool> WaitForJoinAsync(
    MessagePackStreamReader reader,
    CancellationToken cancellationToken)
{
    while (true)
    {
        var response = await ReadMessageAsync<IServerMessage>(reader, cancellationToken).ConfigureAwait(false);
        if (response == null)
        {
            Console.WriteLine("Disconnected before join.");
            return false;
        }

        if (response is LobbyJoined joined)
        {
            Console.WriteLine($"Joined lobby {joined.RoomId}");
            return true;
        }

        if (response is ErrorResponse error)
        {
            Console.WriteLine($"Lobby error: {error.Code}");
            return false;
        }
    }
}

static void SendClientMessage(NetworkStream stream, IClientMessage message)
{
    MessagePackSerializer.Serialize(stream, message);
    stream.Flush();
}

static async Task<T?> ReadMessageAsync<T>(MessagePackStreamReader reader, CancellationToken cancellationToken) where T : class
{
    var msgpack = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
    if (msgpack == null)
    {
        return null;
    }

    return MessagePackSerializer.Deserialize<T>(msgpack.Value, cancellationToken: cancellationToken);
}
