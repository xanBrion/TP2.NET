using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using gameServer.ClientsHandling;
using gameServer.GameHandling;

namespace gameServer.ServerHandling
{
    internal class Room
    {
        private const int MaxPlayers = 10;
        private const int TickIntervalMs = 50;
        private const int SyncIntervalMs = 1000;

        private readonly Dictionary<int, Player> _playersById = new Dictionary<int, Player>();
        private readonly Dictionary<int, Observer> _observersById = new Dictionary<int, Observer>();
        private readonly object _playersLock = new object();

        private Game? _game;
        private CancellationTokenSource? _gameLoopCts;
        private Task? _gameLoopTask;

        public int Id { get; }

        public Room(int id)
        {
            Id = id;
        }

        public int PlayerCount
        {
            get
            {
                lock (_playersLock)
                {
                    return _playersById.Count;
                }
            }
        }

        public int Capacity => MaxPlayers;

        public int ObserverCount
        {
            get
            {
                lock (_playersLock)
                {
                    return _observersById.Count;
                }
            }
        }

        public int ParticipantCount
        {
            get
            {
                lock (_playersLock)
                {
                    return _playersById.Count + _observersById.Count;
                }
            }
        }

        public bool IsFull
        {
            get
            {
                lock (_playersLock)
                {
                    return _playersById.Count >= MaxPlayers;
                }
            }
        }

        public bool AddPlayer(Player player)
        {
            lock (_playersLock)
            {
                if (_playersById.Count >= MaxPlayers || _playersById.ContainsKey(player.Id))
                {
                    return false;
                }

                player.PositionX = 0.0f;
                player.PositionY = 0.0f;
                player.IsAlive = true;
                player.IsReady = false;
                _playersById.Add(player.Id, player);

                _game ??= new Game(Id);
                _game.AddPlayer(player);
                Console.WriteLine($"[Room] {Id} : Player {player.Id} joined.");
                EnsureGameLoopStarted_NoLock();
                return true;
            }
        }

        public void RemovePlayer(int playerId)
        {
            lock (_playersLock)
            {
                _playersById.Remove(playerId);
                _game?.RemovePlayer(playerId);
            }
        }

        public bool AddObserver(Observer observer)
        {
            lock (_playersLock)
            {
                if (_observersById.ContainsKey(observer.Id))
                {
                    return false;
                }

                _observersById.Add(observer.Id, observer);
                EnsureGameLoopStarted_NoLock();
                return true;
            }
        }

        public void RemoveObserver(int observerId)
        {
            lock (_playersLock)
            {
                _observersById.Remove(observerId);
            }
        }

        public WorldStateUpdate BuildWorldStateSnapshot(long serverTimeMs)
        {
            lock (_playersLock)
            {
                if (_game == null)
                {
                    return new WorldStateUpdate
                    {
                        RoomId = Id,
                        ServerTimeMs = serverTimeMs
                    };
                }

                var snapshot = _game.BuildSnapshot();
                return BuildWorldUpdateFromTick(snapshot, serverTimeMs);
            }
        }

        public void HandleMessage(Player player, IClientMessage message)
        {
            switch (message)
            {
                case PlayerReadyUpdate readyUpdate:
                    lock (_playersLock)
                    {
                        if (_playersById.TryGetValue(player.Id, out var readyPlayer))
                        {
                            readyPlayer.IsReady = readyUpdate.IsReady;
                            Console.WriteLine(
                                $"[Room] {Id} : Player {readyPlayer.Id} ready = {readyPlayer.IsReady}");
                        }
                    }
                    break;

                case PlayerPositionUpdate positionUpdate:
                    lock (_playersLock)
                    {
                        if (_game == null || !_playersById.TryGetValue(player.Id, out var trackedPlayer))
                        {
                            return;
                        }

                        if (!trackedPlayer.IsAlive || !AllPlayersReady_NoLock())
                        {
                            return;
                        }

                        trackedPlayer.PositionX = positionUpdate.X;
                        trackedPlayer.PositionY = positionUpdate.Y;
                        _game.UpdatePlayerPosition(trackedPlayer.Id, positionUpdate.X, positionUpdate.Y);
                    }
                    break;
            }
        }

        public void Stop()
        {
            CancellationTokenSource? cts;
            lock (_playersLock)
            {
                cts = _gameLoopCts;
                _gameLoopCts = null;
                _gameLoopTask = null;
            }

            cts?.Cancel();
            cts?.Dispose();
        }

        private void EnsureGameLoopStarted_NoLock()
        {
            if (_gameLoopTask != null)
            {
                return;
            }

            _gameLoopCts = new CancellationTokenSource();
            _gameLoopTask = Task.Run(() => RunGameLoopAsync(_gameLoopCts.Token));
        }

        private async Task RunGameLoopAsync(CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            long previousStepMs = stopwatch.ElapsedMilliseconds;
            long lastSyncSentMs = 0;
            bool matchFinishedNotified = false;
            var defeatedNotifiedIds = new HashSet<int>();
            int lastReadyPlayers = -1;
            int lastTotalPlayers = -1;
            bool lastAllReady = false;
            bool lastCanStart = false;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    long nowStepMs = stopwatch.ElapsedMilliseconds;
                    float deltaSeconds = Math.Max(0.0f, (nowStepMs - previousStepMs) / 1000.0f);
                    previousStepMs = nowStepMs;
                    long serverTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    GameTickResult? tick = null;
                    List<Player>? players = null;
                    List<Observer>? observers = null;
                    int readyPlayers = 0;
                    int totalPlayers = 0;
                    bool allReady = false;
                    bool canStart = false;

                    lock (_playersLock)
                    {
                        if (_game == null)
                        {
                            break;
                        }

                        if (_playersById.Count == 0 && _observersById.Count == 0)
                        {
                            break;
                        }

                        totalPlayers = _playersById.Count;
                        readyPlayers = _playersById.Values.Count(p => p.IsReady);
                        allReady = totalPlayers > 0 && readyPlayers == totalPlayers;
                        canStart = totalPlayers >= 2 && allReady;

                        tick = canStart ? _game.Tick(deltaSeconds) : _game.BuildSnapshot();

                        foreach (var state in tick.PlayerStates)
                        {
                            if (_playersById.TryGetValue(state.PlayerId, out var connected))
                            {
                                connected.PositionX = state.X;
                                connected.PositionY = state.Y;
                                connected.IsAlive = state.IsAlive;
                                connected.IsReady = state.IsReady;
                            }
                        }

                        players = _playersById.Values.ToList();
                        observers = _observersById.Values.ToList();
                    }

                    if (tick == null || players == null || observers == null)
                    {
                        break;
                    }

                    var playersById = players.ToDictionary(p => p.Id);
                    if (readyPlayers != lastReadyPlayers
                        || totalPlayers != lastTotalPlayers
                        || allReady != lastAllReady
                        || canStart != lastCanStart)
                    {
                        lastReadyPlayers = readyPlayers;
                        lastTotalPlayers = totalPlayers;
                        lastAllReady = allReady;
                        lastCanStart = canStart;
                        SendToParticipants(players, observers, new RoomReadinessUpdate
                        {
                            RoomId = Id,
                            ReadyPlayers = readyPlayers,
                            TotalPlayers = totalPlayers,
                            AllReady = allReady,
                            CanStart = canStart
                        });
                    }

                    if (serverTimeMs - lastSyncSentMs >= SyncIntervalMs)
                    {
                        var sync = new ServerTimeSync
                        {
                            RoomId = Id,
                            ServerTimeMs = serverTimeMs
                        };
                        SendToParticipants(players, observers, sync);
                        lastSyncSentMs = serverTimeMs;
                    }

                    foreach (var mob in tick.SpawnedMobs)
                    {
                        var spawned = new MobSpawned
                        {
                            RoomId = Id,
                            ServerTimeMs = serverTimeMs,
                            Mob = CloneMobState(mob)
                        };
                        SendToParticipants(players, observers, spawned);
                    }

                    var worldUpdate = BuildWorldUpdateFromTick(tick, serverTimeMs);
                    SendToParticipants(players, observers, worldUpdate);

                    foreach (int defeatedPlayerId in tick.DefeatedPlayerIds)
                    {
                        if (defeatedNotifiedIds.Contains(defeatedPlayerId))
                        {
                            continue;
                        }

                        defeatedNotifiedIds.Add(defeatedPlayerId);
                        if (playersById.TryGetValue(defeatedPlayerId, out var defeatedPlayer))
                        {
                            defeatedPlayer.SendMessage<IServerMessage>(new PlayerOutcome
                            {
                                RoomId = Id,
                                ServerTimeMs = serverTimeMs,
                                PlayerId = defeatedPlayerId,
                                Outcome = "defeat"
                            });
                        }
                    }

                    if (tick.MatchFinished && !matchFinishedNotified)
                    {
                        matchFinishedNotified = true;
                        string winnerPseudo = "";

                        if (tick.WinnerPlayerId >= 0 && playersById.TryGetValue(tick.WinnerPlayerId, out var winner))
                        {
                            winnerPseudo = winner.Pseudo;
                            winner.SendMessage<IServerMessage>(new PlayerOutcome
                            {
                                RoomId = Id,
                                ServerTimeMs = serverTimeMs,
                                PlayerId = winner.Id,
                                Outcome = "victory"
                            });
                        }

                        Console.WriteLine(
                            $"[Room] {Id} : Match finished. Winner = {winnerPseudo} ({tick.WinnerPlayerId})");

                        SendToParticipants(players, observers, new MatchFinished
                        {
                            RoomId = Id,
                            ServerTimeMs = serverTimeMs,
                            WinnerPlayerId = tick.WinnerPlayerId,
                            WinnerPseudo = winnerPseudo
                        });
                    }

                    try
                    {
                        await Task.Delay(TickIntervalMs, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
            finally
            {
                CancellationTokenSource? ctsToDispose = null;
                lock (_playersLock)
                {
                    ctsToDispose = _gameLoopCts;
                    _gameLoopCts = null;
                    _gameLoopTask = null;
                }

                ctsToDispose?.Dispose();
            }
        }

        private WorldStateUpdate BuildWorldUpdateFromTick(GameTickResult tick, long serverTimeMs)
        {
            return new WorldStateUpdate
            {
                RoomId = Id,
                ServerTimeMs = serverTimeMs,
                Players = tick.PlayerStates.Select(ClonePlayerState).ToList(),
                Mobs = tick.MobStates.Select(CloneMobState).ToList()
            };
        }

        private static PlayerStateData ClonePlayerState(PlayerStateData player)
        {
            return new PlayerStateData
            {
                PlayerId = player.PlayerId,
                Pseudo = player.Pseudo,
                X = player.X,
                Y = player.Y,
                IsAlive = player.IsAlive,
                IsReady = player.IsReady
            };
        }

        private static MobStateData CloneMobState(MobStateData mob)
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

        private static void SendToParticipants(
            IEnumerable<Player> players,
            IEnumerable<Observer> observers,
            IServerMessage message)
        {
            foreach (var player in players)
            {
                TrySend(() => player.SendMessage<IServerMessage>(message));
            }

            foreach (var observer in observers)
            {
                TrySend(() => observer.SendMessage<IServerMessage>(message));
            }
        }

        private static void TrySend(Action send)
        {
            try
            {
                send();
            }
            catch
            {
            }
        }

        private bool AllPlayersReady_NoLock()
        {
            return _playersById.Count > 0 && _playersById.Values.All(p => p.IsReady);
        }
    }
}
