using System;
using System.Globalization;
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
            Console.WriteLine("1) set ready");
            Console.WriteLine("2) set unready");
            Console.WriteLine("3) send position");
            Console.WriteLine("4) wait for server update");
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
                case "ready":
                    SendClientMessage(stream, new PlayerReadyUpdate { IsReady = true });
                    await TryReadAndPrintServerEventAsync(reader, TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);
                    break;
                case "2":
                case "unready":
                    SendClientMessage(stream, new PlayerReadyUpdate { IsReady = false });
                    await TryReadAndPrintServerEventAsync(reader, TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);
                    break;
                case "3":
                case "position":
                {
                    Console.Write("x> ");
                    var xText = (Console.ReadLine() ?? "").Trim();
                    Console.Write("y> ");
                    var yText = (Console.ReadLine() ?? "").Trim();

                    if (!TryParseFloat(xText, out var x) || !TryParseFloat(yText, out var y))
                    {
                        Console.WriteLine("Invalid coordinates.");
                        break;
                    }

                    SendClientMessage(stream, new PlayerPositionUpdate
                    {
                        X = x,
                        Y = y,
                        ClientTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    });

                    await TryReadAndPrintServerEventAsync(reader, TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);
                    break;
                }
                case "4":
                case "wait":
                {
                    await TryReadAndPrintServerEventAsync(reader, TimeSpan.FromSeconds(5)).ConfigureAwait(false);
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

    await TryReadAndPrintServerEventAsync(reader, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

    while (true)
    {
        Console.WriteLine("\nObserver menu:");
        Console.WriteLine("1) wait for next update");
        Console.WriteLine("0) quit");
        Console.Write("choice> ");

        var input = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        if (input == "0" || input == "quit")
        {
            break;
        }

        await TryReadAndPrintServerEventAsync(reader, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
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

static async Task TryReadAndPrintServerEventAsync(MessagePackStreamReader reader, TimeSpan timeout)
{
    var response = await ReadMessageAsync<IServerMessage>(reader, timeout).ConfigureAwait(false);
    if (response == null)
    {
        Console.WriteLine("No server event.");
        return;
    }

    switch (response)
    {
        case WorldStateUpdate world:
            Console.WriteLine(
                $"World t={world.ServerTimeMs} room={world.RoomId} players={world.Players.Count} mobs={world.Mobs.Count}");
            break;
        case MobSpawned spawned:
            Console.WriteLine(
                $"Mob spawned id={spawned.Mob.MobId} x={spawned.Mob.X:F1} y={spawned.Mob.Y:F1} v=({spawned.Mob.VelocityX:F1},{spawned.Mob.VelocityY:F1})");
            break;
        case ServerTimeSync sync:
            Console.WriteLine($"Server time sync room={sync.RoomId} t={sync.ServerTimeMs}");
            break;
        case RoomReadinessUpdate readiness:
            Console.WriteLine(
                $"Ready room={readiness.RoomId} ready={readiness.ReadyPlayers}/{readiness.TotalPlayers} allReady={readiness.AllReady} canStart={readiness.CanStart}");
            break;
        case PlayerOutcome outcome:
            Console.WriteLine($"Outcome room={outcome.RoomId} player={outcome.PlayerId} -> {outcome.Outcome}");
            break;
        case MatchFinished finished:
            var winnerDisplay = string.IsNullOrWhiteSpace(finished.WinnerPseudo)
                ? $"#{finished.WinnerPlayerId}"
                : finished.WinnerPseudo;
            Console.WriteLine($"Match finished room={finished.RoomId} winner={winnerDisplay}");
            break;
        case ErrorResponse error:
            Console.WriteLine($"Server error: {error.Code}");
            break;
        default:
            Console.WriteLine($"Received: {response.GetType().Name}");
            break;
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

static bool TryParseFloat(string text, out float value)
{
    if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
    {
        return true;
    }

    return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
}
