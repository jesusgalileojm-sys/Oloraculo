namespace Oloraculo.Web.Models
{
    public class MatchResult
    {
        public required string Id { get; set; }
        public string HomeTeamId { get; set; }
        public string AwayTeamId { get; set; }
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }
        public DateTimeOffset Date { get; set; }
        public string Tournament { get; set; }
        public bool Neutral { get; set; }
        public string Source { get; set; }
    }
}
