using System.Collections.Generic;
using MessagePack;

namespace gameServer.ServerHandling
{
    [Union(0, typeof(LobbyListResponse))]
    [Union(1, typeof(LobbyJoined))]
    [Union(2, typeof(ErrorResponse))]
    [Union(3, typeof(ObserverJoined))]
    [Union(4, typeof(ServerTimeSync))]
    [Union(5, typeof(WorldStateUpdate))]
    [Union(6, typeof(MobSpawned))]
    [Union(7, typeof(PlayerOutcome))]
    [Union(8, typeof(MatchFinished))]
    [Union(9, typeof(RoomReadinessUpdate))]
    internal interface IServerMessage
    {
    }

    [Union(0, typeof(ClientPseudoResponse))]
    [Union(1, typeof(QuickJoinRequest))]
    [Union(2, typeof(LobbyListRequest))]
    [Union(3, typeof(LobbyJoinRequest))]
    [Union(4, typeof(LobbyCreateRequest))]
    [Union(5, typeof(ObserverConnectRequest))]
    [Union(6, typeof(PlayerPositionUpdate))]
    [Union(7, typeof(PlayerReadyUpdate))]
    internal interface IClientMessage
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
    internal sealed class ObserverConnectRequest : IClientMessage
    {
        [Key(0)]
        public int RoomId { get; set; }
    }

    [MessagePackObject]
    internal sealed class PlayerPositionUpdate : IClientMessage
    {
        [Key(0)]
        public float X { get; set; }

        [Key(1)]
        public float Y { get; set; }

        [Key(2)]
        public long ClientTimeMs { get; set; }
    }

    [MessagePackObject]
    internal sealed class PlayerReadyUpdate : IClientMessage
    {
        [Key(0)]
        public bool IsReady { get; set; }
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
    internal sealed class ServerTimeSync : IServerMessage
    {
        [Key(0)]
        public int RoomId { get; set; }

        [Key(1)]
        public long ServerTimeMs { get; set; }
    }

    [MessagePackObject]
    internal sealed class RoomReadinessUpdate : IServerMessage
    {
        [Key(0)]
        public int RoomId { get; set; }

        [Key(1)]
        public int ReadyPlayers { get; set; }

        [Key(2)]
        public int TotalPlayers { get; set; }

        [Key(3)]
        public bool AllReady { get; set; }

        [Key(4)]
        public bool CanStart { get; set; }
    }

    [MessagePackObject]
    internal sealed class PlayerStateData
    {
        [Key(0)]
        public int PlayerId { get; set; }

        [Key(1)]
        public string Pseudo { get; set; } = "";

        [Key(2)]
        public float X { get; set; }

        [Key(3)]
        public float Y { get; set; }

        [Key(4)]
        public bool IsAlive { get; set; }

        [Key(5)]
        public bool IsReady { get; set; }
    }

    [MessagePackObject]
    internal sealed class WorldStateUpdate : IServerMessage
    {
        [Key(0)]
        public int RoomId { get; set; }

        [Key(1)]
        public long ServerTimeMs { get; set; }

        [Key(2)]
        public List<PlayerStateData> Players { get; set; } = new List<PlayerStateData>();

        [Key(3)]
        public List<MobStateData> Mobs { get; set; } = new List<MobStateData>();
    }

    [MessagePackObject]
    internal sealed class MobSpawned : IServerMessage
    {
        [Key(0)]
        public int RoomId { get; set; }

        [Key(1)]
        public long ServerTimeMs { get; set; }

        [Key(2)]
        public MobStateData Mob { get; set; } = new MobStateData();
    }

    [MessagePackObject]
    internal sealed class PlayerOutcome : IServerMessage
    {
        [Key(0)]
        public int RoomId { get; set; }

        [Key(1)]
        public long ServerTimeMs { get; set; }

        [Key(2)]
        public int PlayerId { get; set; }

        [Key(3)]
        public string Outcome { get; set; } = "";
    }

    [MessagePackObject]
    internal sealed class MatchFinished : IServerMessage
    {
        [Key(0)]
        public int RoomId { get; set; }

        [Key(1)]
        public long ServerTimeMs { get; set; }

        [Key(2)]
        public int WinnerPlayerId { get; set; }
    }
}
