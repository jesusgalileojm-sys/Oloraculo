namespace Oloraculo.Web.Models.ApiFootballModels
{
    public static class ApiFootballEndpoints
    {
        public const string SquadSource = "players/squads";

        public static string LeagueCoverage(int leagueId, int season) =>
            $"leagues?id={leagueId}&season={season}";

        public static string FixtureInjuries(string externalFixtureId) =>
            $"injuries?fixture={externalFixtureId}";

        public static string LeagueInjuries(int leagueId, int season) =>
            $"injuries?league={leagueId}&season={season}";

        public static string FixtureLineups(string externalFixtureId) =>
            $"fixtures/lineups?fixture={externalFixtureId}";

        public static string PreMatchOdds(string externalFixtureId) =>
            $"odds?fixture={externalFixtureId}";

        public static string LiveOdds(string externalFixtureId) =>
            $"odds/live?fixture={externalFixtureId}";

        public static string Fixtures(int leagueId, int season, string timezone = "UTC") =>
            $"fixtures?league={leagueId}&season={season}&timezone={timezone}";

        public static string Teams(int leagueId, int season) =>
            $"teams?league={leagueId}&season={season}";

        public static string Squad(long teamId) =>
            $"players/squads?team={teamId}";

        public static string PlayersByTeamSeason(long teamId, int season) =>
            $"players?team={teamId}&season={season}";

        public static string PlayersByTeamSeasonSource(int season) =>
            $"players season {season}";
    }
}
