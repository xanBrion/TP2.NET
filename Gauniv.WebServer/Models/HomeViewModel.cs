using Gauniv.WebServer.Dtos;

namespace Gauniv.WebServer.Models
{
    public class HomeViewModel
    {
        public List<GameDto> Games { get; set; } = new List<GameDto>();
        public string? FilterName { get; set; }
        public decimal? FilterMinPrice { get; set; }
        public decimal? FilterMaxPrice { get; set; }
        public string? FilterCategory { get; set; }
        public bool? FilterOwned { get; set; }
        public List<string> AvailableCategories { get; set; } = new List<string>();
    }
}
