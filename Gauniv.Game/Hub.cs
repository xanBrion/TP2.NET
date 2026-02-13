using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Hub : Control
{
	[Export]
	public string ServerHost { get; set; } = "127.0.0.1";

	[Export]
	public int ServerPort { get; set; } = 13000;

	[Export]
	public double LobbyRefreshSeconds { get; set; } = 2.0;

	private ItemList _roomsList;
	private Button _createButton;
	private Button _joinButton;
	private Button _quikJoinButton;

	private GameServerNetworkClient _networkClient;
	private bool _connected;
	private double _refreshAccumulator;

	private readonly object _incomingLock = new object();
	private readonly Queue<IServerMessage> _incomingMessages = new Queue<IServerMessage>();
	private readonly List<LobbyInfo> _rooms = new List<LobbyInfo>();

	public override void _Ready()
	{
		_roomsList = GetNode<ItemList>("RoomsList");
		_createButton = GetNode<Button>("Create");
		_joinButton = GetNode<Button>("Join");
		_quikJoinButton = GetNode<Button>("QuickJoin");
		_roomsList.Clear();
		_roomsList.AddItem("Connecting to server...");
		_ = ConnectAndStartRefreshAsync();
	}

	public override void _Process(double delta)
	{
		DrainServerMessages();

		if (!_connected || _networkClient == null)
		{
			return;
		}

		_refreshAccumulator += delta;
		if (_refreshAccumulator < LobbyRefreshSeconds)
		{
			return;
		}

		_refreshAccumulator = 0;
		_ = _networkClient.RequestLobbyListAsync();
	}

	public override void _ExitTree()
	{
		_networkClient?.Dispose();
		_networkClient = null;
	}

	private async Task ConnectAndStartRefreshAsync()
	{
		_networkClient = new GameServerNetworkClient();
		_networkClient.ServerMessageReceived += OnServerMessageReceived;
		_networkClient.ErrorOccurred += message => GD.PrintErr($"[Hub] Network error: {message}");

		try
		{
			string pseudo = $"Hub_{(int)(GD.Randi() % 10000)}";
			await _networkClient.ConnectAsPlayerAsync(ServerHost, ServerPort, pseudo).ConfigureAwait(false);
			_connected = true;
			await _networkClient.RequestLobbyListAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[Hub] Failed to connect to server: {ex.Message}");
			_roomsList.Clear();
			_roomsList.AddItem("Server unavailable");
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
			IServerMessage message;
			lock (_incomingLock)
			{
				if (_incomingMessages.Count == 0)
				{
					return;
				}

				message = _incomingMessages.Dequeue();
			}

			switch (message)
			{
				case LobbyListResponse list:
					ApplyLobbyList(list.Lobbies);
					break;
				case ErrorResponse error:
					GD.PrintErr($"[Hub] Server error: {error.Code}");
					break;
			}
		}
	}

	private void ApplyLobbyList(List<LobbyInfo> lobbies)
	{
		int previouslySelectedRoomId = -1;
		var selected = _roomsList.GetSelectedItems();
		if (selected.Length > 0 && selected[0] >= 0 && selected[0] < _rooms.Count)
		{
			previouslySelectedRoomId = _rooms[selected[0]].Id;
		}

		_rooms.Clear();
		_rooms.AddRange(lobbies);
		_roomsList.Clear();

		if (_rooms.Count == 0)
		{
			_roomsList.AddItem("No lobbies available");
			return;
		}

		for (int i = 0; i < _rooms.Count; i++)
		{
			var lobby = _rooms[i];
			_roomsList.AddItem($"Room {lobby.Id} - {lobby.PlayerCount}/{lobby.Capacity}");
		}

		if (previouslySelectedRoomId > 0)
		{
			for (int i = 0; i < _rooms.Count; i++)
			{
				if (_rooms[i].Id == previouslySelectedRoomId)
				{
					_roomsList.Select(i);
					break;
				}
			}
		}
	}

	private void OnCreateRoom()
	{
		GameLaunchContext.CreateRoomRequested = true;
		GameLaunchContext.PreferredRoomId = -1;
		ChangeToGameScene();
	}

	private void OnJoinRoom()
	{
		var selected = _roomsList.GetSelectedItems();
		if (selected.Length == 0)
		{
			GD.Print("No room selected");
			return;
		}

		int index = selected[0];
		if (index < 0 || index >= _rooms.Count)
		{
			GD.Print("Selected row is not a lobby");
			return;
		}

		int roomId = _rooms[index].Id;
		GameLaunchContext.CreateRoomRequested = false;
		GameLaunchContext.PreferredRoomId = roomId;
		ChangeToGameScene();
	}

	private void OnQuickJoinRoom()
	{
		if (_rooms.Count == 0)
		{
			GD.Print("No rooms available");
			return;
		}

		foreach (LobbyInfo room in _rooms)
		{
			if (room.PlayerCount < room.Capacity)
			{
				GameLaunchContext.CreateRoomRequested = false;
				GameLaunchContext.PreferredRoomId = room.Id;
				ChangeToGameScene();
				return;
			}
		}
	}

	private void OnQuit()
	{
		GetTree().Quit();
	}

	private void ChangeToGameScene()
	{
		var err = GetTree().ChangeSceneToFile("res://main.tscn");
		if (err != Error.Ok)
		{
			GD.PrintErr($"Error to load game scene: {err}");
		}
	}
}
