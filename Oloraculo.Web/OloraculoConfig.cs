namespace Oloraculo.Web
{
    public class OloraculoConfig
    {
        public int SimulationCount { get; set; }
        public int? SimulationSeed { get; set; }
        public int RecentResultCount { get; set; }
        public int GoalModelYearsWindow { get; set; }
        public string ApiFootballBaseUrl { get; set; } = "https://v3.football.api-sports.io/";
        public string? ApiFootballApiKey { get; set; }
        public int ApiFootballLeagueId { get; set; }
        public int ApiFootballSeason { get; set; }
    }
}
