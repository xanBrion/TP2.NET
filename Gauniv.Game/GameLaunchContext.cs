public static class GameLaunchContext
{
    public static int PreferredRoomId { get; set; } = -1;

    public static bool CreateRoomRequested { get; set; }

    public static bool ObserveRoomRequested { get; set; }

    public static string PreferredPseudo { get; set; } = string.Empty;
}
