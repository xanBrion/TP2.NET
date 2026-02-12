using MessagePack;

[Union(0, typeof(ServerPseudoRequest))]
[Union(1, typeof(LobbyListResponse))]
[Union(2, typeof(LobbyJoined))]
[Union(3, typeof(ErrorResponse))]
[Union(4, typeof(ObserverJoined))]
[Union(5, typeof(GameStateSnapshot))]
public interface IServerMessage
{
}

[Union(0, typeof(ClientPseudoResponse))]
[Union(1, typeof(QuickJoinRequest))]
[Union(2, typeof(LobbyListRequest))]
[Union(3, typeof(LobbyJoinRequest))]
[Union(4, typeof(LobbyCreateRequest))]
[Union(5, typeof(PlayerDisplacement))]
[Union(6, typeof(ObserverConnectRequest))]
public interface IClientMessage
{
}

[MessagePackObject]
public sealed class ServerPseudoRequest : IServerMessage
{
}

[MessagePackObject]
public sealed class ClientPseudoResponse : IClientMessage
{
    [Key(0)]
    public string Pseudo { get; set; } = "";
}

[MessagePackObject]
public sealed class QuickJoinRequest : IClientMessage
{
}

[MessagePackObject]
public sealed class LobbyListRequest : IClientMessage
{
}

[MessagePackObject]
public sealed class LobbyJoinRequest : IClientMessage
{
    [Key(0)]
    public int RoomId { get; set; }
}

[MessagePackObject]
public sealed class LobbyCreateRequest : IClientMessage
{
}

[MessagePackObject]
public sealed class PlayerDisplacement : IClientMessage
{
    [Key(0)]
    public string Payload { get; set; } = "";
}

[MessagePackObject]
public sealed class ObserverConnectRequest : IClientMessage
{
    [Key(0)]
    public int RoomId { get; set; }
}

[MessagePackObject]
public sealed class LobbyJoined : IServerMessage
{
    [Key(0)]
    public int RoomId { get; set; }
}

[MessagePackObject]
public sealed class ErrorResponse : IServerMessage
{
    [Key(0)]
    public string Code { get; set; } = "";
}

[MessagePackObject]
public sealed class ObserverJoined : IServerMessage
{
    [Key(0)]
    public int RoomId { get; set; }
}

[MessagePackObject]
public sealed class GameStateSnapshot : IServerMessage
{
    [Key(0)]
    public int RoomId { get; set; }

    [Key(1)]
    public string State { get; set; } = "";
}
