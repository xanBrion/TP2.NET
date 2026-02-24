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

	private string _pseudo = string.Empty;
	private int _currentRoomId = -1;
	private int _localPlayerId = -1;
	private bool _isObserverMode;
	private bool _serverMatchStarted;
	private bool _serverMatchEnded;
	private bool _localDefeatConfirmedByServer;
	private bool _localDefeatUiShown;
	private long _lastPositionSentMs;

	private GameServerNetworkClient? _networkClient;

	private readonly object _incomingLock = new object();
	private readonly Queue<IServerMessage> _incomingMessages = new Queue<IServerMessage>();
	private readonly Dictionary<int, Mob> _serverMobsById = new Dictionary<int, Mob>();
	private readonly Dictionary<int, Player> _remotePlayersById = new Dictionary<int, Player>();
	private static readonly PackedScene RemotePlayerScene = GD.Load<PackedScene>("res://player.tscn");

	public override void _Ready()
	{
		GetNode<Hud>("HUD").HideMatchEndScreen();

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
		if (UseServerNetworking)
		{
			// In network mode the server is authoritative for death events.
			if (!_localDefeatConfirmedByServer || _localDefeatUiShown)
			{
				return;
			}

			_localDefeatUiShown = true;
			var hud = GetNode<Hud>("HUD");
			hud.ShowEliminated();

			var player = GetNode<Player>("Player");
			player.Hide();
			var collisionShape = player.GetNode<CollisionShape2D>("CollisionShape2D");
			collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
			GetNode<AudioStreamPlayer>("DeathSound").Play();
			return;
		}

		GetNode<Timer>("MobTimer").Stop();
		GetNode<Hud>("HUD").ShowGameOver();

		GetNode<AudioStreamPlayer>("Music").Stop();
		GetNode<AudioStreamPlayer>("DeathSound").Play();
	}

	public void NewGame()
	{
		if (UseServerNetworking)
		{
			if (_isObserverMode)
			{
				GetNode<Hud>("HUD").ShowMessage("Observer mode");
				return;
			}

			if (_networkClient == null || !_networkClient.IsConnected)
			{
				GD.PrintErr("[Network] Not connected to game server.");
				return;
			}

			GetNode<Hud>("HUD").ShowMessage("Ready sent");
			_ = _networkClient.SetReadyAsync(true);
			return;
		}

		var hud = GetNode<Hud>("HUD");
		hud.ShowMessage("Get Ready!");

		var player = GetNode<Player>("Player");
		player.IgnoreLocalHits = false;
		var startPosition = GetNode<Marker2D>("StartPosition");
		player.Start(startPosition.Position);

		GetTree().CallGroup("mobs", Node.MethodName.QueueFree);

		GetNode<Timer>("StartTimer").Start();
		GetNode<AudioStreamPlayer>("Music").Play();
	}

	private void OnStartTimerTimeout()
	{
		if (UseServerNetworking)
		{
			return;
		}

		GetNode<Timer>("MobTimer").Start();
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
		_networkClient = new GameServerNetworkClient();
		_networkClient.ServerMessageReceived += OnServerMessageReceived;
		_networkClient.ErrorOccurred += message => GD.PrintErr($"[Network] {message}");

		try
		{
			int preferredRoomId = GameLaunchContext.PreferredRoomId;
			bool createRoom = GameLaunchContext.CreateRoomRequested;
			bool observeRoom = GameLaunchContext.ObserveRoomRequested;
			var preferredPseudo = GameLaunchContext.PreferredPseudo.Trim();
			_pseudo = string.IsNullOrWhiteSpace(preferredPseudo)
				? $"Godot_{(int)(GD.Randi() % 10000)}"
				: preferredPseudo;

			GameLaunchContext.PreferredRoomId = -1;
			GameLaunchContext.CreateRoomRequested = false;
			GameLaunchContext.ObserveRoomRequested = false;

			if (observeRoom && preferredRoomId > 0)
			{
				_isObserverMode = true;
				await _networkClient.ConnectAsObserverAsync(ServerHost, ServerPort, preferredRoomId)
					.ConfigureAwait(false);

				var localPlayer = GetNode<Player>("Player");
				localPlayer.Hide();
				localPlayer.GetNode<CollisionShape2D>("CollisionShape2D").Disabled = true;
			}
			else
			{
				_isObserverMode = false;
				await _networkClient.ConnectAsPlayerAsync(ServerHost, ServerPort, _pseudo).ConfigureAwait(false);
				await _networkClient.RequestLobbyListAsync().ConfigureAwait(false);

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
				_isObserverMode = false;
				_serverMatchStarted = false;
				_serverMatchEnded = false;
				_localDefeatConfirmedByServer = false;
				_localDefeatUiShown = false;
				_localPlayerId = -1;
				GetNode<Hud>("HUD").HideMatchEndScreen();
				GetNode<Hud>("HUD").ShowMessage($"Joined room {_currentRoomId}. Press Start to ready.");
				break;

			case ObserverJoined joined:
				_currentRoomId = joined.RoomId;
				_isObserverMode = true;
				_serverMatchStarted = false;
				_serverMatchEnded = false;
				_localDefeatConfirmedByServer = false;
				_localDefeatUiShown = false;
				_localPlayerId = -1;
				GetNode<Hud>("HUD").HideMatchEndScreen();
				GetNode<Hud>("HUD").ShowMessage($"Observing room {_currentRoomId}");
				break;

			case RoomReadinessUpdate readiness:
				if (readiness.RoomId != _currentRoomId)
				{
					return;
				}

				GetNode<Hud>("HUD").ShowMessage($"Ready {readiness.ReadyPlayers}/{readiness.TotalPlayers}");
				if (!_isObserverMode && readiness.CanStart && !_serverMatchStarted)
				{
					StartServerDrivenMatch();
				}
				break;

			case WorldStateUpdate world:
				if (world.RoomId == _currentRoomId && !_serverMatchEnded)
				{
					ApplyWorldState(world);
				}
				break;

			case PlayerOutcome outcome:
				if (outcome.PlayerId == _localPlayerId && outcome.Outcome == "defeat")
				{
					_localDefeatConfirmedByServer = true;
					GameOver();
				}
				if (outcome.PlayerId == _localPlayerId && outcome.Outcome == "victory")
				{
					GetNode<Hud>("HUD").ShowMessage("Victory");
				}
				break;

			case MatchFinished finished:
				_serverMatchEnded = true;
				_serverMatchStarted = false;
				StopServerDrivenMatchVisuals();

				var winnerLabel = string.IsNullOrWhiteSpace(finished.WinnerPseudo)
					? $"#{finished.WinnerPlayerId}"
					: finished.WinnerPseudo;
				GetNode<Hud>("HUD").ShowMatchEndScreen(
					winnerLabel,
					finished.WinnerPlayerId == _localPlayerId);
				break;

			case ErrorResponse error:
				GD.PrintErr($"[Network] Server error: {error.Code}");
				break;
		}
	}

	private void StartServerDrivenMatch()
	{
		_serverMatchStarted = true;
		_serverMatchEnded = false;
		_localDefeatConfirmedByServer = false;
		_localDefeatUiShown = false;
		_lastPositionSentMs = 0;
		GetNode<Hud>("HUD").HideMatchEndScreen();

		var player = GetNode<Player>("Player");
		player.IgnoreLocalHits = true;
		var startPosition = GetNode<Marker2D>("StartPosition");
		player.Start(startPosition.Position);

		ClearServerMobs();
		ClearRemotePlayers();
		GetTree().CallGroup("mobs", Node.MethodName.QueueFree);

		GetNode<Timer>("MobTimer").Stop();
		GetNode<Timer>("StartTimer").Stop();
	}

	private void StopServerDrivenMatchVisuals()
	{
		ClearServerMobs();
		ClearRemotePlayers();

		var player = GetNode<Player>("Player");
		var collisionShape = player.GetNode<CollisionShape2D>("CollisionShape2D");
		collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
	}

	private void OnBackToLobby()
	{
		_networkClient?.Dispose();
		_networkClient = null;
		_isObserverMode = false;
		_serverMatchStarted = false;
		_serverMatchEnded = false;
		_localDefeatConfirmedByServer = false;
		_localDefeatUiShown = false;
		_localPlayerId = -1;
		_currentRoomId = -1;
		GameLaunchContext.CreateRoomRequested = false;
		GameLaunchContext.ObserveRoomRequested = false;
		GameLaunchContext.PreferredRoomId = -1;

		var err = GetTree().ChangeSceneToFile("res://Hub.tscn");
		if (err != Error.Ok)
		{
			GD.PrintErr($"Error to load hub scene: {err}");
		}
	}

	private void ApplyWorldState(WorldStateUpdate world)
	{
		if (_networkClient == null)
		{
			return;
		}

		if (!_isObserverMode && _localPlayerId <= 0)
		{
			var meByPseudo = world.Players.FirstOrDefault(p => p.Pseudo == _networkClient.Pseudo);
			if (meByPseudo != null)
			{
				_localPlayerId = meByPseudo.PlayerId;
			}
		}

		if (!_isObserverMode && _localPlayerId > 0)
		{
			var me = world.Players.FirstOrDefault(p => p.PlayerId == _localPlayerId);
			if (me != null)
			{
				var player = GetNode<Player>("Player");
				if (me.IsAlive && !player.Visible)
				{
					player.Start(new Vector2(me.X, me.Y));
				}

				if (me.IsAlive)
				{
					player.Position = new Vector2(me.X, me.Y);
				}
				else
				{
					player.Hide();
				}

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
				remotePlayer = RemotePlayerScene.Instantiate<Player>();
				remotePlayer.IsLocallyControlled = false;
				remotePlayer.IsRed = true;
				remotePlayer.IgnoreLocalHits = true;
				AddChild(remotePlayer);
				remotePlayer.Start(new Vector2(playerState.X, playerState.Y));
				remotePlayer.GetNode<CollisionShape2D>("CollisionShape2D").Disabled = true;
				_remotePlayersById[playerState.PlayerId] = remotePlayer;
			}

			remotePlayer.Position = new Vector2(playerState.X, playerState.Y);
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
		if (_isObserverMode || !_serverMatchStarted || _networkClient == null || !_networkClient.IsConnected)
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
