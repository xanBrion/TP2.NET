using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main : Node
{
	[Export]
	public PackedScene MobScene { get; set; }

	[Export]
	public bool UseServerNetworking { get; set; } = true;

	[Export]
	public string ServerHost { get; set; } = "127.0.0.1";

	[Export]
	public int ServerPort { get; set; } = 13000;

	private int _score;
	private string _pseudo = string.Empty;
	private int _currentRoomId = -1;
	private int _localPlayerId = -1;
	private bool _serverMatchStarted;
	private long _lastPositionSentMs;

	private GameServerNetworkClient? _networkClient;

	private readonly object _incomingLock = new object();
	private readonly Queue<IServerMessage> _incomingMessages = new Queue<IServerMessage>();
	private readonly Dictionary<int, Mob> _serverMobsById = new Dictionary<int, Mob>();
	private readonly Dictionary<int, RemotePlayerView> _remotePlayersById = new Dictionary<int, RemotePlayerView>();

	public override void _Ready()
	{
		if (UseServerNetworking)
		{
			StartServerSession();
		}
	}

	public override void _Process(double delta)
	{
		if (!UseServerNetworking)
		{
			return;
		}

		DrainServerMessages();
		SendLocalPositionToServer();
	}

	public override void _ExitTree()
	{
		_networkClient?.Dispose();
		_networkClient = null;
		ClearRemotePlayers();
	}

	public void GameOver()
	{
		GetNode<Timer>("MobTimer").Stop();
		GetNode<Timer>("ScoreTimer").Stop();
		GetNode<Hud>("HUD").ShowGameOver();

		GetNode<AudioStreamPlayer>("Music").Stop();
		GetNode<AudioStreamPlayer>("DeathSound").Play();
	}

	public void NewGame()
	{
		if (UseServerNetworking)
		{
			if (_networkClient == null || !_networkClient.IsConnected)
			{
				GD.PrintErr("[Network] Not connected to game server.");
				return;
			}

			GetNode<Hud>("HUD").ShowMessage("Ready sent");
			_ = _networkClient.SetReadyAsync(true);
			return;
		}

		_score = 0;

		var hud = GetNode<Hud>("HUD");
		hud.UpdateScore(_score);
		hud.ShowMessage("Get Ready!");

		var player = GetNode<Player>("Player");
		var startPosition = GetNode<Marker2D>("StartPosition");
		player.Start(startPosition.Position);

		GetTree().CallGroup("mobs", Node.MethodName.QueueFree);

		GetNode<Timer>("StartTimer").Start();
		GetNode<AudioStreamPlayer>("Music").Play();
	}

	private void OnScoreTimerTimeout()
	{
		if (UseServerNetworking)
		{
			return;
		}

		_score++;
		GetNode<Hud>("HUD").UpdateScore(_score);
	}

	private void OnStartTimerTimeout()
	{
		if (UseServerNetworking)
		{
			return;
		}

		GetNode<Timer>("MobTimer").Start();
		GetNode<Timer>("ScoreTimer").Start();
	}

	private void OnMobTimerTimeout()
	{
		if (UseServerNetworking)
		{
			return;
		}

		Mob mob = MobScene.Instantiate<Mob>();

		var mobSpawnLocation = GetNode<PathFollow2D>("MobPath/MobSpawnLocation");
		mobSpawnLocation.ProgressRatio = GD.Randf();

		float direction = mobSpawnLocation.Rotation + Mathf.Pi / 2;
		mob.Position = mobSpawnLocation.Position;
		direction += (float)GD.RandRange(-Mathf.Pi / 4, Mathf.Pi / 4);
		mob.Rotation = direction;

		var velocity = new Vector2((float)GD.RandRange(150.0, 250.0), 0);
		mob.LinearVelocity = velocity.Rotated(direction);

		AddChild(mob);
	}

	private async void StartServerSession()
	{
		_pseudo = $"Godot_{(int)(GD.Randi() % 10000)}";
		_networkClient = new GameServerNetworkClient();
		_networkClient.ServerMessageReceived += OnServerMessageReceived;
		_networkClient.ErrorOccurred += message => GD.PrintErr($"[Network] {message}");

		try
		{
			await _networkClient.ConnectAsPlayerAsync(ServerHost, ServerPort, _pseudo).ConfigureAwait(false);
			await _networkClient.RequestLobbyListAsync().ConfigureAwait(false);

			int preferredRoomId = GameLaunchContext.PreferredRoomId;
			bool createRoom = GameLaunchContext.CreateRoomRequested;
			GameLaunchContext.PreferredRoomId = -1;
			GameLaunchContext.CreateRoomRequested = false;

			if (createRoom)
			{
				await _networkClient.CreateLobbyAsync().ConfigureAwait(false);
			}
			else if (preferredRoomId > 0)
			{
				await _networkClient.JoinLobbyAsync(preferredRoomId).ConfigureAwait(false);
			}
			else
			{
				await _networkClient.QuickJoinAsync().ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Network] Connection failed: {ex.Message}");
		}
	}

	private void OnServerMessageReceived(IServerMessage message)
	{
		lock (_incomingLock)
		{
			_incomingMessages.Enqueue(message);
		}
	}

	private void DrainServerMessages()
	{
		while (true)
		{
			IServerMessage? message = null;
			lock (_incomingLock)
			{
				if (_incomingMessages.Count == 0)
				{
					break;
				}

				message = _incomingMessages.Dequeue();
			}

			if (message != null)
			{
				HandleServerMessage(message);
			}
		}
	}

	private void HandleServerMessage(IServerMessage message)
	{
		switch (message)
		{
			case LobbyListResponse lobbies:
				GD.Print($"[Network] Lobbies found: {lobbies.Lobbies.Count}");
				break;

			case LobbyJoined joined:
				_currentRoomId = joined.RoomId;
				GetNode<Hud>("HUD").ShowMessage($"Joined room {_currentRoomId}. Press Start to ready.");
				break;

			case RoomReadinessUpdate readiness:
				if (readiness.RoomId != _currentRoomId)
				{
					return;
				}

				GetNode<Hud>("HUD").ShowMessage($"Ready {readiness.ReadyPlayers}/{readiness.TotalPlayers}");
				if (readiness.CanStart && !_serverMatchStarted)
				{
					StartServerDrivenMatch();
				}
				break;

			case WorldStateUpdate world:
				if (world.RoomId == _currentRoomId)
				{
					ApplyWorldState(world);
				}
				break;

			case PlayerOutcome outcome:
				if (outcome.PlayerId == _localPlayerId && outcome.Outcome == "defeat")
				{
					GameOver();
				}
				if (outcome.PlayerId == _localPlayerId && outcome.Outcome == "victory")
				{
					GetNode<Hud>("HUD").ShowMessage("Victory");
				}
				break;

			case MatchFinished finished:
				var winnerLabel = string.IsNullOrWhiteSpace(finished.WinnerPseudo)
					? $"#{finished.WinnerPlayerId}"
					: finished.WinnerPseudo;
				GetNode<Hud>("HUD").ShowMessage($"Match finished. Winner: {winnerLabel}");
				break;

			case ErrorResponse error:
				GD.PrintErr($"[Network] Server error: {error.Code}");
				break;
		}
	}

	private void StartServerDrivenMatch()
	{
		_serverMatchStarted = true;
		_score = 0;
		GetNode<Hud>("HUD").UpdateScore(_score);

		var player = GetNode<Player>("Player");
		var startPosition = GetNode<Marker2D>("StartPosition");
		player.Start(startPosition.Position);

		ClearServerMobs();
		ClearRemotePlayers();
		GetTree().CallGroup("mobs", Node.MethodName.QueueFree);

		GetNode<Timer>("MobTimer").Stop();
		GetNode<Timer>("ScoreTimer").Stop();
		GetNode<Timer>("StartTimer").Stop();
	}

	private void ApplyWorldState(WorldStateUpdate world)
	{
		if (_networkClient == null)
		{
			return;
		}

		if (_localPlayerId <= 0)
		{
			var meByPseudo = world.Players.FirstOrDefault(p => p.Pseudo == _networkClient.Pseudo);
			if (meByPseudo != null)
			{
				_localPlayerId = meByPseudo.PlayerId;
			}
		}

		if (_localPlayerId > 0)
		{
			var me = world.Players.FirstOrDefault(p => p.PlayerId == _localPlayerId);
			if (me != null)
			{
				var player = GetNode<Player>("Player");
				if (!player.Visible)
				{
					player.Start(new Vector2(me.X, me.Y));
				}

				player.Position = new Vector2(me.X, me.Y);
				if (!me.IsAlive)
				{
					GameOver();
				}
			}
		}

		var presentRemotePlayerIds = new HashSet<int>();
		foreach (var playerState in world.Players)
		{
			if (playerState.PlayerId == _localPlayerId || !playerState.IsAlive)
			{
				continue;
			}

			presentRemotePlayerIds.Add(playerState.PlayerId);
			if (!_remotePlayersById.TryGetValue(playerState.PlayerId, out var remotePlayer)
				|| !IsInstanceValid(remotePlayer))
			{
				remotePlayer = new RemotePlayerView();
				AddChild(remotePlayer);
				_remotePlayersById[playerState.PlayerId] = remotePlayer;
			}

			remotePlayer.Position = new Vector2(playerState.X, playerState.Y);
			remotePlayer.UpdateFromState(playerState);
		}

		var staleRemotePlayerIds = _remotePlayersById.Keys
			.Where(id => !presentRemotePlayerIds.Contains(id))
			.ToList();
		foreach (var staleId in staleRemotePlayerIds)
		{
			if (_remotePlayersById.TryGetValue(staleId, out var staleRemote)
				&& IsInstanceValid(staleRemote))
			{
				staleRemote.QueueFree();
			}

			_remotePlayersById.Remove(staleId);
		}

		var presentMobIds = new HashSet<int>();
		foreach (var mobState in world.Mobs)
		{
			presentMobIds.Add(mobState.MobId);
			if (!_serverMobsById.TryGetValue(mobState.MobId, out var mob) || !IsInstanceValid(mob))
			{
				mob = MobScene.Instantiate<Mob>();
				mob.Freeze = true;
				AddChild(mob);
				_serverMobsById[mobState.MobId] = mob;
			}

			mob.Position = new Vector2(mobState.X, mobState.Y);
			mob.Rotation = mobState.Angle;
			mob.LinearVelocity = new Vector2(mobState.VelocityX, mobState.VelocityY);
		}

		var staleMobIds = _serverMobsById.Keys.Where(id => !presentMobIds.Contains(id)).ToList();
		foreach (var staleId in staleMobIds)
		{
			if (_serverMobsById.TryGetValue(staleId, out var staleMob) && IsInstanceValid(staleMob))
			{
				staleMob.QueueFree();
			}

			_serverMobsById.Remove(staleId);
		}
	}

	private void SendLocalPositionToServer()
	{
		if (!_serverMatchStarted || _networkClient == null || !_networkClient.IsConnected)
		{
			return;
		}

		long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		if (now - _lastPositionSentMs < 50)
		{
			return;
		}

		_lastPositionSentMs = now;
		var player = GetNode<Player>("Player");
		if (!player.Visible)
		{
			return;
		}

		_ = _networkClient.SendPositionAsync(player.Position.X, player.Position.Y);
	}

	private void ClearServerMobs()
	{
		foreach (var mob in _serverMobsById.Values)
		{
			if (IsInstanceValid(mob))
			{
				mob.QueueFree();
			}
		}

		_serverMobsById.Clear();
	}

	private void ClearRemotePlayers()
	{
		foreach (var remotePlayer in _remotePlayersById.Values)
		{
			if (IsInstanceValid(remotePlayer))
			{
				remotePlayer.QueueFree();
			}
		}

		_remotePlayersById.Clear();
	}
}
