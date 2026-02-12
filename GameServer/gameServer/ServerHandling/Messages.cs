using System.Collections.Generic;
using MessagePack;

namespace gameServer.ServerHandling
{
    [Union(0, typeof(ServerPseudoRequest))]
    [Union(1, typeof(LobbyListResponse))]
    [Union(2, typeof(LobbyJoined))]
    [Union(3, typeof(ErrorResponse))]
    [Union(4, typeof(ObserverJoined))]
    [Union(5, typeof(GameStateSnapshot))]
    internal interface IServerMessage
    {
    }

    [Union(0, typeof(ClientPseudoResponse))]
    [Union(1, typeof(QuickJoinRequest))]
    [Union(2, typeof(LobbyListRequest))]
    [Union(3, typeof(LobbyJoinRequest))]
    [Union(4, typeof(LobbyCreateRequest))]
    [Union(5, typeof(PlayerDisplacement))]
    [Union(6, typeof(ObserverConnectRequest))]
    internal interface IClientMessage
    {
    }

    [MessagePackObject]
    internal sealed class ServerPseudoRequest : IServerMessage
    {
    }

    [MessagePackObject]
    internal sealed class ClientPseudoResponse : IClientMessage
    {
        [Key(0)]
        public string Pseudo { get; set; } = "";
    }

    [MessagePackObject]
    internal sealed class QuickJoinRequest : IClientMessage
    {
    }

    [MessagePackObject]
    internal sealed class LobbyListRequest : IClientMessage
    {
    }

    [MessagePackObject]
    internal sealed class LobbyJoinRequest : IClientMessage
    {
        [Key(0)]
        public int RoomId { get; set; }
    }

    [MessagePackObject]
    internal sealed class LobbyCreateRequest : IClientMessage
    {
    }

    [MessagePackObject]
    internal sealed class PlayerDisplacement : IClientMessage
    {
        [Key(0)]
        public string Payload { get; set; } = "";
    }

    [MessagePackObject]
    internal sealed class ObserverConnectRequest : IClientMessage
    {
        [Key(0)]
        public int RoomId { get; set; }
    }

    [MessagePackObject]
    internal sealed class LobbyInfo
    {
        [Key(0)]
        public int Id { get; set; }

        [Key(1)]
        public int PlayerCount { get; set; }

        [Key(2)]
        public int Capacity { get; set; }
    }

    [MessagePackObject]
    internal sealed class LobbyListResponse : IServerMessage
    {
        [Key(0)]
        public List<LobbyInfo> Lobbies { get; set; } = new List<LobbyInfo>();
    }

    [MessagePackObject]
    internal sealed class LobbyJoined : IServerMessage
    {
        [Key(0)]
        public int RoomId { get; set; }
    }

    [MessagePackObject]
    internal sealed class ErrorResponse : IServerMessage
    {
        [Key(0)]
        public string Code { get; set; } = "";
    }

    [MessagePackObject]
    internal sealed class ObserverJoined : IServerMessage
    {
        [Key(0)]
        public int RoomId { get; set; }
    }

    [MessagePackObject]
    internal sealed class GameStateSnapshot : IServerMessage
    {
        [Key(0)]
        public int RoomId { get; set; }

        [Key(1)]
        public string State { get; set; } = "";
    }
}
