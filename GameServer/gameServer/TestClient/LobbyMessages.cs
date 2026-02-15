using System.Collections.Generic;
using MessagePack;

[MessagePackObject]
public sealed class LobbyInfo
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    public int PlayerCount { get; set; }

    [Key(2)]
    public int Capacity { get; set; }
}

[MessagePackObject]
public sealed class LobbyListResponse : IServerMessage
{
    [Key(0)]
    public List<LobbyInfo> Lobbies { get; set; } = new List<LobbyInfo>();
}
