using System;
using System.Collections.Generic;
using gameServer.ClientsHandling;

namespace gameServer.ServerHandling
{
    internal sealed class RoomManager
    {
        private readonly Dictionary<int, Room> _roomsById = new Dictionary<int, Room>();
        private readonly object _roomsLock = new object();
        private int _nextRoomId = 1;

        public Room QuickJoin(Player player)
        {
            lock (_roomsLock)
            {
                foreach (var room in _roomsById.Values)
                {
                    if (room.IsFull)
                    {
                        continue;
                    }

                    room.AddPlayer(player);
                    return room;
                }

                var newRoom = new Room(_nextRoomId++);
                newRoom.AddPlayer(player);
                _roomsById.Add(newRoom.Id, newRoom);
                return newRoom;
            }
        }

        public bool TryJoinRoom(int roomId, Player player, out Room room)
        {
            lock (_roomsLock)
            {
                if (_roomsById.TryGetValue(roomId, out room) && !room.IsFull)
                {
                    room.AddPlayer(player);
                    return true;
                }
            }

            room = null!;
            return false;
        }

        public Room CreateRoomAndJoin(Player player)
        {
            lock (_roomsLock)
            {
                var newRoom = new Room(_nextRoomId++);
                newRoom.AddPlayer(player);
                _roomsById.Add(newRoom.Id, newRoom);
                return newRoom;
            }
        }

        public List<LobbyInfo> GetLobbyList()
        {
            var list = new List<LobbyInfo>();
            lock (_roomsLock)
            {
                foreach (var room in _roomsById.Values)
                {
                    list.Add(new LobbyInfo
                    {
                        Id = room.Id,
                        PlayerCount = room.PlayerCount,
                        Capacity = room.Capacity
                    });
                }
            }

            return list;
        }

        public void HandlePlayerDisconnected(Player player, Room room)
        {
            room.RemovePlayer(player.Id);
            Console.WriteLine($"[RoomManager] Player {player.Id} : Disconnected from room {room.Id}");

            lock (_roomsLock)
            {
                if (room.ParticipantCount == 0)
                {
                    _roomsById.Remove(room.Id);
                    Console.WriteLine($"[RoomManager] Room {room.Id} : Deleted (empty)");
                }
            }
        }

        public bool TryAttachObserver(int roomId, Observer observer, out Room room)
        {
            lock (_roomsLock)
            {
                if (_roomsById.TryGetValue(roomId, out room))
                {
                    return room.AddObserver(observer);
                }
            }

            room = null!;
            return false;
        }

        public void HandleObserverDisconnected(Observer observer, Room room)
        {
            room.RemoveObserver(observer.Id);
            Console.WriteLine($"[RoomManager] Observer {observer.Id} : Disconnected from room {room.Id}");

            lock (_roomsLock)
            {
                if (room.ParticipantCount == 0)
                {
                    _roomsById.Remove(room.Id);
                    Console.WriteLine($"[RoomManager] Room {room.Id} : Deleted (empty)");
                }
            }
        }
    }
}
