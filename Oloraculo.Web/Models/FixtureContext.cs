namespace Oloraculo.Web.Models
{
    public class FixtureContext
    {
        public required string FixtureId { get; set; }
        public int UnavailableHomePlayers { get; set; }
        public int UnavailableAwayPlayers { get; set; }
        public bool HasLineups { get; set; }
        public bool HasOdds { get; set; }
        public bool HasAvailabilityNews { get; set; }
        public string Notes { get; set; } = "";
        public DateTimeOffset UpdatedAt { get; set; }

    }
}
