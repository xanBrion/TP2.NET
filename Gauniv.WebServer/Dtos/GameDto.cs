namespace Gauniv.WebServer.Dtos
{

    public class GameDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public long PayloadSize { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public bool Owned { get; set; } = false;
    }

    public class CreateGameDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public IFormFile PayloadFile { get; set; } = null!;
        public List<string> Categories { get; set; } = new List<string>();
    }

    public class UpdateGameDto
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal? Price { get; set; }
        public IFormFile? PayloadFile { get; set; }
        public List<string>? Categories { get; set; }
    }
}