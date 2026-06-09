namespace Oloraculo.Web.Models
{
    public class Group
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public IReadOnlyList<string> TeamIds { get; init; } = Array.Empty<string>();
        public string? Source { get; set; }
    }
}
