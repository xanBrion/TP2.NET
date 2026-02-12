using Gauniv.WebServer.Dtos;

namespace Gauniv.WebServer.Models
{
    public class HomeViewModel
    {
        public List<GameDto> Games { get; set; } = new List<GameDto>();
        public StatsViewModel? Stats { get; set; }
        public string? FilterName { get; set; }
        public decimal? FilterMinPrice { get; set; }
        public decimal? FilterMaxPrice { get; set; }
        public string? FilterCategory { get; set; }
        public bool? FilterOwned { get; set; }
        public List<string> AvailableCategories { get; set; } = new List<string>();
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
