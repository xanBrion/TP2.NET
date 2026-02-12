using Godot;
using System;
using System.Collections.Generic;

public partial class Hub : Control
{
	private ItemList _roomsList;
	private Button _createButton;
	private Button _joinButton;

	private List<string> _rooms = new();
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

	private void AddRoom(string name)
	{
		_rooms.Add(name);
		_roomsList.AddItem(name);
	}

	private void OnCreateRoom()
	{
		string newRoom = $"Room {_roomCounter}";
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
		string roomName = _rooms[index];

		GD.Print($"Join room : {roomName}");

		string gamePath = "res://main.tscn";
		var err = GetTree().ChangeSceneToFile(gamePath);
		if (err != Error.Ok)
		{
			GD.PrintErr($"Error to charge game : {err}");
		}
	}
}
