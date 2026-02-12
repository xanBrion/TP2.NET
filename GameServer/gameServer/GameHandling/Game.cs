using System;
using System.Collections.Generic;
using System.Linq;
using gameServer.ClientsHandling;
using gameServer.ServerHandling;

namespace gameServer.GameHandling
{
    internal sealed class Game
    {
        private const float PlaneWidth = 1280.0f;
        private const float PlaneHeight = 720.0f;
        private const float MobSpawnIntervalSeconds = 0.75f;
        private const float MobMinSpeed = 150.0f;
        private const float MobMaxSpeed = 250.0f;
        private const float MobDirectionRandomness = MathF.PI / 4.0f;
        private const float CollisionRadius = 24.0f;
        private const float OutOfBoundsMargin = 120.0f;

        private readonly Random _random = new Random();
        private readonly Dictionary<int, Player> _playersById = new Dictionary<int, Player>();
        private readonly Dictionary<int, MobStateData> _mobsById = new Dictionary<int, MobStateData>();

        private float _spawnAccumulator;
        private int _nextMobId = 1;
        private bool _isMatchFinished;
        private int _winnerPlayerId = -1;

        public Game(int roomId)
        {
            RoomId = roomId;
            StartedAtUtc = DateTime.UtcNow;
        }

        public int RoomId { get; }

        public DateTime StartedAtUtc { get; }

        public void AddPlayer(Player player)
        {
            _playersById[player.Id] = player;
        }

        public void RemovePlayer(int playerId)
        {
            _playersById.Remove(playerId);
            EvaluateWinner();
        }

        public void UpdatePlayerPosition(int playerId, float x, float y)
        {
            if (_playersById.TryGetValue(playerId, out var player) && player.IsAlive)
            {
                player.PositionX = x;
                player.PositionY = y;
            }
        }

        public GameTickResult Tick(float deltaSeconds)
        {
            var result = new GameTickResult();

            if (!_isMatchFinished)
            {
                SpawnMobs(deltaSeconds, result);
                MoveMobs(deltaSeconds);
                ResolveCollisions(result);
                EvaluateWinner();
            }

            result.MatchFinished = _isMatchFinished;
            result.WinnerPlayerId = _winnerPlayerId;
            result.PlayerStates = _playersById.Values.Select(ClonePlayer).ToList();
            result.MobStates = _mobsById.Values.Select(CloneMob).ToList();
            return result;
        }

        public GameTickResult BuildSnapshot()
        {
            return new GameTickResult
            {
                MatchFinished = _isMatchFinished,
                WinnerPlayerId = _winnerPlayerId,
                PlayerStates = _playersById.Values.Select(ClonePlayer).ToList(),
                MobStates = _mobsById.Values.Select(CloneMob).ToList()
            };
        }

        private void SpawnMobs(float deltaSeconds, GameTickResult result)
        {
            _spawnAccumulator += deltaSeconds;

            while (_spawnAccumulator >= MobSpawnIntervalSeconds)
            {
                _spawnAccumulator -= MobSpawnIntervalSeconds;
                var mob = CreateMob();
                _mobsById[mob.MobId] = mob;
                result.SpawnedMobs.Add(CloneMob(mob));
            }
        }

        private MobStateData CreateMob()
        {
            float x;
            float y;
            int border = _random.Next(4);

            switch (border)
            {
                case 0:
                    x = NextFloat(0.0f, PlaneWidth);
                    y = -8.0f;
                    break;
                case 1:
                    x = PlaneWidth + 8.0f;
                    y = NextFloat(0.0f, PlaneHeight);
                    break;
                case 2:
                    x = NextFloat(0.0f, PlaneWidth);
                    y = PlaneHeight + 8.0f;
                    break;
                default:
                    x = -8.0f;
                    y = NextFloat(0.0f, PlaneHeight);
                    break;
            }

            float towardCenter = MathF.Atan2((PlaneHeight * 0.5f) - y, (PlaneWidth * 0.5f) - x);
            float angle = towardCenter + NextFloat(-MobDirectionRandomness, MobDirectionRandomness);
            float speed = NextFloat(MobMinSpeed, MobMaxSpeed);
            float velocityX = MathF.Cos(angle) * speed;
            float velocityY = MathF.Sin(angle) * speed;

            return new MobStateData
            {
                MobId = _nextMobId++,
                X = x,
                Y = y,
                Speed = speed,
                Angle = angle,
                VelocityX = velocityX,
                VelocityY = velocityY
            };
        }

        private void MoveMobs(float deltaSeconds)
        {
            var toRemove = new List<int>();

            foreach (var mob in _mobsById.Values)
            {
                mob.X += mob.VelocityX * deltaSeconds;
                mob.Y += mob.VelocityY * deltaSeconds;

                bool isOutside = mob.X < -OutOfBoundsMargin
                    || mob.X > PlaneWidth + OutOfBoundsMargin
                    || mob.Y < -OutOfBoundsMargin
                    || mob.Y > PlaneHeight + OutOfBoundsMargin;

                if (isOutside)
                {
                    toRemove.Add(mob.MobId);
                }
            }

            foreach (int mobId in toRemove)
            {
                _mobsById.Remove(mobId);
            }
        }

        private void ResolveCollisions(GameTickResult result)
        {
            float hitDistanceSquared = CollisionRadius * CollisionRadius;

            foreach (var player in _playersById.Values)
            {
                if (!player.IsAlive)
                {
                    continue;
                }

                foreach (var mob in _mobsById.Values)
                {
                    float dx = player.PositionX - mob.X;
                    float dy = player.PositionY - mob.Y;
                    float distanceSquared = (dx * dx) + (dy * dy);
                    if (distanceSquared <= hitDistanceSquared)
                    {
                        player.IsAlive = false;
                        result.DefeatedPlayerIds.Add(player.Id);
                        break;
                    }
                }
            }
        }

        private void EvaluateWinner()
        {
            if (_isMatchFinished)
            {
                return;
            }

            if (_playersById.Count < 2)
            {
                return;
            }

            var alive = _playersById.Values.Where(p => p.IsAlive).Select(p => p.Id).ToList();
            if (alive.Count > 1)
            {
                return;
            }

            _isMatchFinished = true;
            _winnerPlayerId = alive.Count == 1 ? alive[0] : -1;
        }

        private float NextFloat(float min, float max)
        {
            return (float)(min + (_random.NextDouble() * (max - min)));
        }

        private static PlayerStateData ClonePlayer(Player player)
        {
            return new PlayerStateData
            {
                PlayerId = player.Id,
                Pseudo = player.Pseudo,
                X = player.PositionX,
                Y = player.PositionY,
                IsAlive = player.IsAlive,
                IsReady = player.IsReady
            };
        }

        private static MobStateData CloneMob(MobStateData mob)
        {
            return new MobStateData
            {
                MobId = mob.MobId,
                X = mob.X,
                Y = mob.Y,
                Speed = mob.Speed,
                Angle = mob.Angle,
                VelocityX = mob.VelocityX,
                VelocityY = mob.VelocityY
            };
        }
    }

    internal sealed class GameTickResult
    {
        public List<MobStateData> SpawnedMobs { get; } = new List<MobStateData>();

        public List<int> DefeatedPlayerIds { get; } = new List<int>();

        public List<PlayerStateData> PlayerStates { get; set; } = new List<PlayerStateData>();

        public List<MobStateData> MobStates { get; set; } = new List<MobStateData>();

        public bool MatchFinished { get; set; }

        public int WinnerPlayerId { get; set; } = -1;
    }
}
