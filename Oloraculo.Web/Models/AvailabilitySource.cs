namespace Oloraculo.Web.Models
{
    public class AvailabilitySource
    {
        public int Id { get; set; }
        public required string Url { get; set; }
        public string? Title { get; set; }
        public string? Publisher { get; set; }
        public int StatusCode { get; set; }
        public string? TextHash { get; set; }
        public DateTimeOffset LastFetchedAt { get; set; }
        public string? Error { get; set; }
    }
}
