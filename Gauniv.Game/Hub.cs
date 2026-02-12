using Godot;
using System;
using System.Collections.Generic;

public partial class Room
{
	public int playerCount;
	public int playerLimit;
	public int id;
}
public partial class Hub : Control
{
	private ItemList _roomsList;
	private Button _createButton;
	private Button _joinButton;

	private List<Room> _rooms = new();
	private int _roomCounter = 1;

	public override void _Ready()
	{
		_roomsList = GetNode<ItemList>("RoomsList");
		_createButton = GetNode<Button>("Create");
		_joinButton = GetNode<Button>("Join");
	}

	public override void _Process(double delta)
	{
	}

	private void AddRoom(Room newRoom)
	{
		_rooms.Add(newRoom);
		var roomName = $"Room {newRoom.id}";
		_roomsList.AddItem(roomName);
	}

	private void OnCreateRoom()
	{
		var newRoom = new Room { id = _roomCounter, playerCount = 0, playerLimit = 4 };
		_roomCounter++;

		AddRoom(newRoom);
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
		string roomName = _rooms[index].id.ToString();

		GD.Print($"Join room : {roomName}");

		string gamePath = "res://main.tscn";
		var err = GetTree().ChangeSceneToFile(gamePath);
		if (err != Error.Ok)
		{
			GD.PrintErr($"Error to charge game : {err}");
		}
	}
}
