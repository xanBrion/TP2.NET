using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

using var client = new TcpClient("127.0.0.1", 13000);
using var stream = client.GetStream();
using var reader = new MessagePackStreamReader(stream);

Console.WriteLine("Choose mode:");
Console.WriteLine("1) player");
Console.WriteLine("2) observer");
Console.Write("choice> ");
var modeInput = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();

if (modeInput == "2" || modeInput == "observer")
{
    await RunObserverModeAsync(reader, stream).ConfigureAwait(false);
    return;
}

await RunPlayerModeAsync(reader, stream).ConfigureAwait(false);

static async Task RunPlayerModeAsync(MessagePackStreamReader reader, NetworkStream stream)
{
    Console.Write("pseudo> ");
    var pseudo = (Console.ReadLine() ?? "").Trim();
    if (string.IsNullOrWhiteSpace(pseudo))
    {
        pseudo = "TestPlayer";
    }

    SendClientMessage(stream, new ClientPseudoResponse { Pseudo = pseudo });

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
            Console.WriteLine("2) set ready");
            Console.WriteLine("3) set not ready");
            Console.WriteLine("4) wait for server event");
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
                    await TryReadAndPrintEventAsync(reader, TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);
                    break;
                }
                case "2":
                case "ready":
                {
                    SendClientMessage(stream, new PlayerReadyUpdate { Ready = true });
                    Console.WriteLine("Ready sent.");
                    await TryReadAndPrintEventAsync(reader, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    break;
                }
                case "3":
                case "unready":
                {
                    SendClientMessage(stream, new PlayerReadyUpdate { Ready = false });
                    Console.WriteLine("Not-ready sent.");
                    await TryReadAndPrintEventAsync(reader, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    break;
                }
                case "4":
                case "wait":
                {
                    await TryReadAndPrintEventAsync(reader, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
                    break;
                }
                default:
                    Console.WriteLine("Unknown choice.");
                    break;
            }
        }
    }
}

static async Task RunObserverModeAsync(MessagePackStreamReader reader, NetworkStream stream)
{
    Console.Write("room id to observe> ");
    var idText = (Console.ReadLine() ?? "").Trim();
    if (!int.TryParse(idText, out var roomId) || roomId <= 0)
    {
        Console.WriteLine("Invalid room id.");
        return;
    }

    SendClientMessage(stream, new ObserverConnectRequest { RoomId = roomId });

    var joinReply = await ReadMessageAsync<IServerMessage>(reader, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
    if (joinReply is ErrorResponse error)
    {
        Console.WriteLine($"Observer error: {error.Code}");
        return;
    }

    if (joinReply is not ObserverJoined joined)
    {
        Console.WriteLine("Did not receive observer join confirmation.");
        return;
    }

    Console.WriteLine($"Observing room {joined.RoomId}.");

    var initialSnapshot = await ReadMessageAsync<IServerMessage>(reader, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
    if (initialSnapshot is GameStateSnapshot initialState)
    {
        Console.WriteLine($"Current state: {initialState.State}");
    }

    while (true)
    {
        Console.WriteLine("\nObserver menu:");
        Console.WriteLine("1) wait for next state update");
        Console.WriteLine("0) quit");
        Console.Write("choice> ");

        var input = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        if (input == "0" || input == "quit")
        {
            break;
        }

        if (input != "1" && input != "wait")
        {
            Console.WriteLine("Unknown choice.");
            continue;
        }

        var response = await ReadMessageAsync<IServerMessage>(reader, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
        if (response == null)
        {
            Console.WriteLine("No update within timeout.");
            continue;
        }

        if (response is GameStateSnapshot snapshot)
        {
            Console.WriteLine($"State update (room {snapshot.RoomId}): {snapshot.State}");
            continue;
        }

        if (response is PlayerReadyChanged readyChanged)
        {
            Console.WriteLine($"Ready changed (room {readyChanged.RoomId}): player {readyChanged.PlayerId} = {readyChanged.Ready}");
            continue;
        }

        if (response is GameStarted started)
        {
            Console.WriteLine($"Game started in room {started.RoomId}");
            continue;
        }

        if (response is ErrorResponse updateError)
        {
            Console.WriteLine($"Server error: {updateError.Code}");
            continue;
        }

        Console.WriteLine($"Received: {response.GetType().Name}");
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

static async Task TryReadAndPrintEventAsync(MessagePackStreamReader reader, TimeSpan timeout)
{
    var response = await ReadMessageAsync<IServerMessage>(reader, timeout).ConfigureAwait(false);
    if (response == null)
    {
        Console.WriteLine("No server event.");
        return;
    }

    if (response is PlayerReadyChanged readyChanged)
    {
        Console.WriteLine($"Ready changed (room {readyChanged.RoomId}): player {readyChanged.PlayerId} = {readyChanged.Ready}");
        return;
    }

    if (response is GameStarted started)
    {
        Console.WriteLine($"Game started in room {started.RoomId}");
        return;
    }

    if (response is GameStateSnapshot snapshot)
    {
        Console.WriteLine($"State update (room {snapshot.RoomId}): {snapshot.State}");
        return;
    }

    if (response is ErrorResponse error)
    {
        Console.WriteLine($"Server error: {error.Code}");
        return;
    }

    Console.WriteLine($"Received: {response.GetType().Name}");
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
