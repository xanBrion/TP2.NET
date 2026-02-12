using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

using var client = new TcpClient("127.0.0.1", 13000);
using var stream = client.GetStream();
using var reader = new MessagePackStreamReader(stream);

await HandlePseudoHandshakeAsync(reader, stream).ConfigureAwait(false);

bool joined = false;
int joinedRoomId = -1;

while (true)
{
    if (!joined)
    {
        Console.WriteLine("\nLobby menu:");
        Console.WriteLine("1) list lobbies");
        Console.WriteLine("2) quick join");
        Console.WriteLine("3) join lobby by id");
        Console.WriteLine("4) create lobby");
        Console.WriteLine("0) quit");
        Console.Write("choice> ");

        var input = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        if (input == "0" || input == "quit")
        {
            break;
        }

        switch (input)
        {
            case "1":
            case "list":
                SendClientMessage(stream, new LobbyListRequest());
                await PrintLobbyListAsync(reader).ConfigureAwait(false);
                break;
            case "2":
            case "quickjoin":
            {
                SendClientMessage(stream, new QuickJoinRequest());
                var joinResult = await WaitForJoinAsync(reader).ConfigureAwait(false);
                if (joinResult.success)
                {
                    joined = true;
                    joinedRoomId = joinResult.roomId;
                }
                break;
            }
            case "3":
            case "join":
            {
                Console.Write("room id> ");
                var idText = (Console.ReadLine() ?? "").Trim();
                if (!int.TryParse(idText, out var roomId) || roomId <= 0)
                {
                    Console.WriteLine("Invalid room id.");
                    break;
                }

                SendClientMessage(stream, new LobbyJoinRequest { RoomId = roomId });
                var joinResult = await WaitForJoinAsync(reader).ConfigureAwait(false);
                if (joinResult.success)
                {
                    joined = true;
                    joinedRoomId = joinResult.roomId;
                }
                break;
            }
            case "4":
            case "create":
            {
                SendClientMessage(stream, new LobbyCreateRequest());
                var joinResult = await WaitForJoinAsync(reader).ConfigureAwait(false);
                if (joinResult.success)
                {
                    joined = true;
                    joinedRoomId = joinResult.roomId;
                }
                break;
            }
            default:
                Console.WriteLine("Unknown choice.");
                break;
        }
    }
    else
    {
        Console.WriteLine($"\nGame menu (room {joinedRoomId}):");
        Console.WriteLine("1) send move");
        Console.WriteLine("0) quit");
        Console.Write("choice> ");

        var input = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        if (input == "0" || input == "quit")
        {
            break;
        }

        switch (input)
        {
            case "1":
            case "move":
            {
                Console.Write("payload> ");
                var payload = (Console.ReadLine() ?? "").Trim();
                SendClientMessage(stream, new PlayerDisplacement { Payload = payload });
                Console.WriteLine("Move sent.");
                break;
            }
            default:
                Console.WriteLine("Unknown choice.");
                break;
        }
    }
}

static async Task HandlePseudoHandshakeAsync(
    MessagePackStreamReader reader,
    NetworkStream stream)
{
    while (true)
    {
        var request = await ReadMessageAsync<IServerMessage>(reader, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        if (request == null)
        {
            throw new InvalidOperationException("Handshake timed out.");
        }

        if (request is ServerPseudoRequest)
        {
            SendClientMessage(stream, new ClientPseudoResponse { Pseudo = "TestPlayer" });
            return;
        }
    }
}

static async Task PrintLobbyListAsync(MessagePackStreamReader reader)
{
    var response = await ReadMessageAsync<IServerMessage>(reader, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
    if (response is not LobbyListResponse lobbyList)
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

static async Task<(bool success, int roomId)> WaitForJoinAsync(MessagePackStreamReader reader)
{
    while (true)
    {
        var response = await ReadMessageAsync<IServerMessage>(reader, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        if (response == null)
        {
            Console.WriteLine("Timed out waiting for server.");
            return (false, -1);
        }

        if (response is LobbyJoined joined)
        {
            Console.WriteLine($"Joined lobby {joined.RoomId}");
            return (true, joined.RoomId);
        }

        if (response is ErrorResponse error)
        {
            Console.WriteLine($"Lobby error: {error.Code}");
            return (false, -1);
        }
    }
}

static void SendClientMessage(NetworkStream stream, IClientMessage message)
{
    MessagePackSerializer.Serialize(stream, message);
    stream.Flush();
}

static async Task<T?> ReadMessageAsync<T>(
    MessagePackStreamReader reader,
    TimeSpan timeout) where T : class
{
    using var cts = new CancellationTokenSource(timeout);
    try
    {
        var msgpack = await reader.ReadAsync(cts.Token).ConfigureAwait(false);
        if (msgpack == null)
        {
            return null;
        }

        return MessagePackSerializer.Deserialize<T>(msgpack.Value, cancellationToken: cts.Token);
    }
    catch (OperationCanceledException)
    {
        return null;
    }
}
