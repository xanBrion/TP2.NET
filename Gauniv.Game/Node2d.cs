using Godot;
using System;

public partial class Node2d : Node2D
{
	private Vector2 position = new Vector2(100, 100);

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{

	}

	public override void _Input(InputEvent @event)
	{
		// Mouse in viewport coordinates.
		if (@event is InputEventMouseButton eventMouseButton)
		{
			GD.Print("Mouse Click/Unclick at: ", eventMouseButton.Position);
		}
		else if (@event is InputEventMouseMotion eventMouseMotion)
		{
			GD.Print("Mouse Motion at: ", eventMouseMotion.Position);
		}

		// Print the size of the viewport.
		GD.Print("Viewport Resolution is: ", GetViewport().GetVisibleRect().Size);
	}
}
