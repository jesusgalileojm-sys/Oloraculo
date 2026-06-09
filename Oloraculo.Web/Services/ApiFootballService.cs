using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Helpers;
using Oloraculo.Web.Models;
using Oloraculo.Web.Models.ApiFootballModels;

namespace Oloraculo.Web.Services
{
    public class ApiFootballService
    {
        private readonly HttpClient _http;
        private readonly OloraculoDbContext _db;
        private readonly OloraculoConfig _config;
        private bool IsConfigured => !string.IsNullOrWhiteSpace(_config.ApiFootballApiKey);
        public ApiFootballService(HttpClient httpClient, OloraculoDbContext db, IOptions<OloraculoConfig> config)
        {
            this._http = httpClient;
            this._db = db;
            this._config = config.Value;
        }

        public Task<ApiFootballRefreshReport> RefreshAsync(string fixtureId, CancellationToken ct = default) =>
            RefreshFixtureContextAsync(fixtureId, ct);

        public async Task<ApiFootballRefreshReport> RefreshFixtureContextAsync(string fixtureId, CancellationToken ct = default)
        {
            if (!IsConfigured)
                return new ApiFootballRefreshReport { IsConfigured = false, Notes = ["API-Football key is not configured."] };

            var errors = new List<string>();
            var notes = new List<string>();
            try
            {
                var fixture = await _db.Fixtures.FindAsync([fixtureId], ct);
                if (fixture is null)
                    return new ApiFootballRefreshReport { IsConfigured = true, Errors = [$"Fixture {fixtureId} was not found."] };

                var mapping = await _db.ApiMappings.SingleOrDefaultAsync(m => m.LocalFixtureId == fixtureId, ct);
                if (mapping is null)
                {
                    var refresh = await RefreshFixturesAsync(ct);
                    mapping = await _db.ApiMappings.SingleOrDefaultAsync(m => m.LocalFixtureId == fixtureId, ct);
                    if (mapping is null)
                        return new ApiFootballRefreshReport { IsConfigured = true, Notes = refresh.Notes, Errors = ["No API fixture mapping found for this local fixture."] };
                }

                var coverage = await GetApiAsync<ApiLeagueResponse>(
                    $"leagues?id={_config.ApiFootballLeagueId}&season={_config.ApiFootballSeason}",
                    "coverage",
                    errors,
                    ct);
                var coverageInfo = coverage?.Response.FirstOrDefault()?.League.Coverage;
                if (coverageInfo is not null)
                    notes.Add($"Coverage says injuries={coverageInfo.Injuries}, odds={coverageInfo.Odds}, lineups={coverageInfo.Fixtures.Lineups}.");

                var fixtureInjuries = await GetApiAsync<ApiInjuryResponse>(
                    $"injuries?fixture={mapping.ExternalFixtureId}",
                    "fixture injuries",
                    errors,
                    ct);
                var leagueInjuries = await GetApiAsync<ApiInjuryResponse>(
                    $"injuries?league={_config.ApiFootballLeagueId}&season={_config.ApiFootballSeason}",
                    "league injuries",
                    errors,
                    ct);
                var lineups = await GetApiAsync<ApiLineupResponse>(
                    $"fixtures/lineups?fixture={mapping.ExternalFixtureId}",
                    "lineups",
                    errors,
                    ct);
                var preMatchOdds = await GetApiAsync<ApiOddsResponse>(
                    $"odds?fixture={mapping.ExternalFixtureId}",
                    "pre-match odds",
                    errors,
                    ct);
                var liveOdds = await GetApiAsync<ApiOddsResponse>(
                    $"odds/live?fixture={mapping.ExternalFixtureId}",
                    "live odds",
                    errors,
                    ct);

                var fixtureInjuryRows = fixtureInjuries?.Response.Count ?? 0;
                var leagueInjuryRows = leagueInjuries?.Response.Count ?? 0;
                var lineupRows = lineups?.Response.Count ?? 0;
                var preMatchOddsRows = preMatchOdds?.Response.Count ?? 0;
                var liveOddsRows = liveOdds?.Response.Count ?? 0;

                var relevantInjuries = MergeRelevantInjuries(fixture, fixtureInjuries?.Response ?? [], leagueInjuries?.Response ?? []);
                var homeUnavailable = relevantInjuries.Count(i => TeamNameNormalizer.ToId(i.Team.Name) == fixture.HomeTeamId);
                var awayUnavailable = relevantInjuries.Count(i => TeamNameNormalizer.ToId(i.Team.Name) == fixture.AwayTeamId);
                var context = await _db.FixtureContexts.FindAsync([fixtureId], ct);
                if (context is null)
                {
                    context = new FixtureContext { FixtureId = fixtureId };
                    _db.FixtureContexts.Add(context);
                }

                context.UnavailableHomePlayers = homeUnavailable;
                context.UnavailableAwayPlayers = awayUnavailable;
                context.HasLineups = lineupRows > 0;
                context.HasOdds = preMatchOddsRows > 0 || liveOddsRows > 0;
                context.Notes = $"Refreshed from API-Football. fixture injuries={fixtureInjuryRows}; league injuries={leagueInjuryRows}; lineups={lineupRows}; pre-match odds={preMatchOddsRows}; live odds={liveOddsRows}.";
                context.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);

                notes.Add($"Fixture injuries rows: {fixtureInjuryRows}. League-season injuries rows: {leagueInjuryRows}. Relevant unavailable/questionable rows stored: home {homeUnavailable}, away {awayUnavailable}.");
                notes.Add($"Lineup rows: {lineupRows}. Pre-match odds rows: {preMatchOddsRows}. Live odds rows: {liveOddsRows}.");
                if (fixtureInjuryRows == 0 && leagueInjuryRows == 0)
                    notes.Add("No injury rows came back. API-Football may support injuries for the competition but still not have absences attached yet.");
                if (preMatchOddsRows == 0)
                    notes.Add("No pre-match odds came back. API-Football documents pre-match odds as limited to the last 7 days.");
                if (liveOddsRows == 0)
                    notes.Add("No live odds came back. That is expected unless the fixture is near kickoff, live, or just finished.");

                return new ApiFootballRefreshReport
                {
                    IsConfigured = true,
                    ContextRows = homeUnavailable + awayUnavailable,
                    FixtureInjuryRows = fixtureInjuryRows,
                    LeagueInjuryRows = leagueInjuryRows,
                    LineupRows = lineupRows,
                    PreMatchOddsRows = preMatchOddsRows,
                    LiveOddsRows = liveOddsRows,
                    Notes = notes,
                    Errors = errors
                };
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
                return new ApiFootballRefreshReport { IsConfigured = true, Errors = errors };
            }
        }
        public async Task<ApiFootballRefreshReport> RefreshFixturesAsync(CancellationToken ct = default)
        {
            if (!IsConfigured)
                return new ApiFootballRefreshReport { IsConfigured = false, Notes = ["API-Football key is not configured. CSV data still works."] };

            var errors = new List<string>();
            var notes = new List<string>();
            try
            {
                var response = await _http.GetFromJsonAsync<ApiFixtureResponse>(
                    $"fixtures?league={_config.ApiFootballLeagueId}&season={_config.ApiFootballSeason}&timezone=UTC", ct);
                var items = response?.Response ?? [];
                var local = await _db.Fixtures.ToListAsync(ct);
                var byPair = local.ToDictionary(f => PairKey(f.HomeTeamId, f.AwayTeamId));
                var matched = 0;

                foreach (var api in items)
                {
                    var home = TeamNameNormalizer.ToId(api.Teams.Home.Name);
                    var away = TeamNameNormalizer.ToId(api.Teams.Away.Name);
                    if (!byPair.TryGetValue(PairKey(home, away), out var fixture))
                        continue;

                    fixture.KickoffUtc = api.Fixture.Date;
                    fixture.Venue = api.Fixture.Venue?.Name;
                    fixture.City = api.Fixture.Venue?.City;
                    fixture.Status = api.Fixture.Status?.Short;
                    fixture.Source = "API-Football";
                    matched++;

                    var existing = await _db.ApiMappings.SingleOrDefaultAsync(m => m.LocalFixtureId == fixture.Id, ct);
                    if (existing is null)
                        _db.ApiMappings.Add(new ApiMapping { LocalFixtureId = fixture.Id, ExternalFixtureId = api.Fixture.Id.ToString() });
                    else
                        existing.ExternalFixtureId = api.Fixture.Id.ToString();
                }

                await _db.SaveChangesAsync(ct);
                notes.Add($"Fetched {items.Count} fixture rows and matched {matched} local group fixtures.");
                return new ApiFootballRefreshReport { IsConfigured = true, FixturesFetched = items.Count, FixturesMatched = matched, Notes = notes };
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
                return new ApiFootballRefreshReport { IsConfigured = true, Errors = errors };
            }
        }
        private async Task<T?> GetApiAsync<T>(string uri, string label, List<string> errors, CancellationToken ct)
        {
            try
            {
                return await _http.GetFromJsonAsync<T>(uri, ct);
            }
            catch (Exception ex)
            {
                errors.Add($"{label}: {ex.Message}");
                return default;
            }
        }
        private static string PairKey(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? $"{a}|{b}" : $"{b}|{a}";
        private static IReadOnlyList<ApiInjury> MergeRelevantInjuries(Fixture fixture, IEnumerable<ApiInjury> fixtureInjuries, IEnumerable<ApiInjury> leagueInjuries)
        {
            var relevant = new Dictionary<string, ApiInjury>();
            foreach (var injury in fixtureInjuries.Concat(leagueInjuries))
            {
                var teamId = TeamNameNormalizer.ToId(injury.Team.Name);
                if (teamId != fixture.HomeTeamId && teamId != fixture.AwayTeamId)
                    continue;

                var playerKey = injury.Player.Id > 0 ? injury.Player.Id.ToString() : injury.Player.Name;
                var key = $"{teamId}|{playerKey}|{injury.Player.Type}|{injury.Player.Reason}";
                relevant.TryAdd(key, injury);
            }

            return relevant.Values.ToList();
        }

    }
}
