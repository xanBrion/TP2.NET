using Godot;

public partial class RemotePlayerView : Node2D
{
	private readonly Label _nameLabel = new Label();
	private string _pseudo = "";
	private bool _isReady;

	public override void _Ready()
	{
		_nameLabel.Position = new Vector2(-40, -32);
		_nameLabel.Modulate = new Color(1f, 1f, 1f, 0.9f);
		AddChild(_nameLabel);
		QueueRedraw();
	}

	public void UpdateFromState(PlayerStateData state)
	{
		_pseudo = state.Pseudo;
		_isReady = state.IsReady;
		_nameLabel.Text = $"{_pseudo}";
		QueueRedraw();
	}

	public override void _Draw()
	{
		Color bodyColor = _isReady
			? new Color(0.25f, 0.8f, 0.35f)
			: new Color(0.95f, 0.75f, 0.25f);
		DrawCircle(Vector2.Zero, 12.0f, bodyColor);
		DrawArc(Vector2.Zero, 14.0f, 0.0f, Mathf.Tau, 24, new Color(0f, 0f, 0f, 0.6f), 2.0f);
	}
}
