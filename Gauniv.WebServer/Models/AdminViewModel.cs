namespace Gauniv.WebServer.Models
{
    public class AdminViewModel
    {
        public StatsViewModel Stats { get; set; } = new StatsViewModel();
        public List<Dtos.GameDto> Games { get; set; } = new();
        public List<string> AvailableCategories { get; set; } = new();

    }

    public class StatsViewModel
    {
        public int TotalGames { get; set; }
        public List<CategoryStatDto> GamesPerCategory { get; set; } = new();
        public double AvgGamesPerUser { get; set; }
        public int MaxSimultaneousPlayers { get; set; }
        public List<GamePlayerStatDto> MaxPlayersPerGame { get; set; } = new();
    }

    public class CategoryStatDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class GamePlayerStatDto
    {
        public string GameName { get; set; } = string.Empty;
        public int MaxPlayers { get; set; }
    }
}
