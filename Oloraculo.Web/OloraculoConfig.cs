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
        public bool RankingRefreshOnStartup { get; set; } = true;
        public int EloRefreshMaxLookbackDays { get; set; } = 14;
        public string FifaRankingsRawUrl { get; set; } = "https://en.wikipedia.org/w/index.php?title=Module:SportsRankings/data/FIFA_World_Rankings&action=raw";
        public string EloRankingsBaseUrl { get; set; } = "https://www.international-football.net/elo-ratings-table";
        public string RankingRefreshUserAgent { get; set; } = "Oloraculo";
    }

    public static class OloraculoDataFiles
    {
        public const string GroupsCsv = "wc2026_groups.csv";
        public const string EloCsv = "elo_snapshot.csv";
        public const string FifaRankingsCsv = "fifa_rankings.csv";
        public const string HistoricalResultsCsv = "historical_results.csv";
    }
}
