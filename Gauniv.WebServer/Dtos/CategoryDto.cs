namespace Gauniv.WebServer.Dtos
{
    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Games { get; set; } = new List<string>();
    }

    public class UpdateCategoryDto
    {
        public string? Name { get; set; }
    }

}
