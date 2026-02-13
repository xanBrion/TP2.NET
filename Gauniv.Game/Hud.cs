using Godot;
using System;

public partial class Hud : CanvasLayer
{
	// Don't forget to rebuild the project so the editor knows about the new signal.

	[Signal]
	public delegate void StartGameEventHandler();

	[Signal]
	public delegate void BackToLobbyEventHandler();

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{

	}

	public void ShowMessage(string text)
	{
		var message = GetNode<Label>("Message");
		message.Text = text;
		message.Show();

		GetNode<Timer>("MessageTimer").Start();
	}

	async public void ShowGameOver()
	{
		HideMatchEndScreen();
		ShowMessage("Game Over");

		var messageTimer = GetNode<Timer>("MessageTimer");
		await ToSignal(messageTimer, Timer.SignalName.Timeout);

		var message = GetNode<Label>("Message");
		message.Text = "Dodge the Creeps!";
		message.Show();

		await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
		GetNode<Button>("StartButton").Show();
	}

	public void ShowEliminated()
	{
		var message = GetNode<Label>("Message");
		message.Text = "Game Over - Spectating";
		message.Show();
	}

	public void ShowMatchEndScreen(string winnerPseudo, bool isLocalWinner)
	{
		var panel = GetNode<Control>("MatchEnd");
		GetNode<Label>("MatchEnd/Panel/Content/Title").Text = isLocalWinner ? "Victory" : "Match Over";
		GetNode<Label>("MatchEnd/Panel/Content/Winner").Text = $"Winner: {winnerPseudo}";
		GetNode<Label>("MatchEnd/Panel/Content/Subtitle").Text = isLocalWinner
			? "You are the last survivor."
			: "Return to lobby for another round.";

		GetNode<Button>("StartButton").Hide();
		GetNode<Label>("Message").Hide();
		panel.Show();
	}

	public void HideMatchEndScreen()
	{
		GetNode<Control>("MatchEnd").Hide();
	}

	public void UpdateScore(int score)
	{
		GetNode<Label>("ScoreLabel").Text = score.ToString();
	}

	// We also specified this function name in PascalCase in the editor's connection window.
	private void OnStartButtonPressed()
	{
		GetNode<Button>("StartButton").Hide();
		EmitSignal(SignalName.StartGame);
	}

	// We also specified this function name in PascalCase in the editor's connection window.
	private void OnMessageTimerTimeout()
	{
		GetNode<Label>("Message").Hide();
	}

	private void OnBackToLobbyPressed()
	{
		EmitSignal(SignalName.BackToLobby);
	}
}
