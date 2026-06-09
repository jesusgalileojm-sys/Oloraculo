using Microsoft.EntityFrameworkCore;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Helpers;
using Oloraculo.Web.Models;
using Oloraculo.Web.Models.CsvModels;
using System.Data;
using System.Globalization;

namespace Oloraculo.Web.Services
{
    public class CsvImportService
    {
        private readonly OloraculoDbContext _db;
        private readonly IWebHostEnvironment _environment;

        public CsvImportService(OloraculoDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _environment = env;
        }

        public async Task ImportIfNeededAsync(CancellationToken ct = default)
        {
            await _db.Database.EnsureCreatedAsync(ct);
            await EnsureFixtureResultColumnsAsync(ct);

            var needsImport =
                !await _db.Groups.AnyAsync(ct) ||
                !await _db.Teams.AnyAsync(ct) ||
                !await _db.Fixtures.AnyAsync(ct) ||
                !await _db.Results.AnyAsync(ct) ||
                !await _db.Ratings.AnyAsync(ct) ||
                await _db.Fixtures.AnyAsync(f => f.Group == "", ct);

            if (needsImport)
                await ImportAllAsync(ct);
        }

        public async Task<CsvImportReport> ImportAllAsync(CancellationToken ct = default)
        {
            await _db.Database.EnsureCreatedAsync(ct);
            await EnsureFixtureResultColumnsAsync(ct);
            await ImportGroupsAsync(ct);
            await ImportRatingsAsync(ct);
            await ImportHistoricalResultsAsync(ct);
            await _db.SaveChangesAsync(ct);
            await GenerateFixturesAsync(ct);
            await _db.SaveChangesAsync(ct);

            return new CsvImportReport
            {
                Groups = await _db.Groups.CountAsync(ct),
                Teams = await _db.Teams.CountAsync(ct),
                Ratings = await _db.Ratings.CountAsync(ct),
                Results = await _db.Results.CountAsync(ct),
                Fixtures = await _db.Fixtures.CountAsync(ct),
            };
        }

        public async Task<int> ImportRatingsOnlyAsync(CancellationToken ct = default)
        {
            await _db.Database.EnsureCreatedAsync(ct);
            await ImportRatingsAsync(ct);
            await _db.SaveChangesAsync(ct);
            return await _db.Ratings.CountAsync(ct);
        }

        private async Task ImportGroupsAsync(CancellationToken ct)
        {
            _db.Groups.RemoveRange(_db.Groups);
            var groupRows = CsvParsingHelper.ReadCsv<GroupCsvRow>(FullPath(OloraculoDataFiles.GroupsCsv));
            var teams = new Dictionary<string, Team>();

            foreach (var row in groupRows)
            {
                var name = TeamNameNormalizer.CanonicalName(row.Team);
                var id = TeamNameNormalizer.ToId(row.Team);
                teams[id] = new Team { Id = id, Name = name, Source = OloraculoDataFiles.GroupsCsv };
            }

            foreach (var team in teams.Values)
            {
                var existing = await _db.Teams.FindAsync([team.Id], ct);
                if (existing is null)
                    _db.Teams.Add(team);
                else
                    existing.Name = team.Name;
            }

            foreach (var group in groupRows.GroupBy(r => r.Group.Trim()).OrderBy(g => g.Key))
            {
                _db.Groups.Add(new Group
                {
                    Name = group.Key,
                    TeamIds = group.Select(r => TeamNameNormalizer.ToId(r.Team)).ToList(),
                    Source = OloraculoDataFiles.GroupsCsv,
                });
            }
        }

        private async Task ImportRatingsAsync(CancellationToken ct)
        {
            _db.Ratings.RemoveRange(_db.Ratings);

            var eloRows = CsvParsingHelper.ReadCsv<EloCsvRow>(FullPath(OloraculoDataFiles.EloCsv));
            foreach (var row in eloRows)
            {
                if (!double.TryParse(row.Elo, NumberStyles.Float, CultureInfo.InvariantCulture, out var elo))
                    continue;

                await CreateTeamIfMissing(row.Team, OloraculoDataFiles.EloCsv, ct);
                _db.Ratings.Add(new Rating
                {
                    TeamId = TeamNameNormalizer.ToId(row.Team),
                    Type = RatingTypeEnum.Elo,
                    Value = elo,
                    AsOf = DateTimeOffset.UtcNow,
                    Source = OloraculoDataFiles.EloCsv
                });
            }

            var fifaRows = CsvParsingHelper.ReadCsv<FifaCsvRow>(FullPath(OloraculoDataFiles.FifaRankingsCsv));
            foreach (var row in fifaRows)
            {
                if (!double.TryParse(row.Points, NumberStyles.Float, CultureInfo.InvariantCulture, out var points))
                    continue;

                await CreateTeamIfMissing(row.Team, OloraculoDataFiles.FifaRankingsCsv, ct);
                _db.Ratings.Add(new Rating
                {
                    TeamId = TeamNameNormalizer.ToId(row.Team),
                    Type = RatingTypeEnum.Fifa,
                    Value = points,
                    AsOf = DateTimeOffset.UtcNow,
                    Source = OloraculoDataFiles.FifaRankingsCsv
                });
            }
        }

        private async Task ImportHistoricalResultsAsync(CancellationToken ct)
        {
            _db.Results.RemoveRange(_db.Results);
            var rows = CsvParsingHelper.ReadCsv<HistoricalResultCsvRow>(FullPath(OloraculoDataFiles.HistoricalResultsCsv));
            var importedIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                if (!DateTimeOffset.TryParse(row.Date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ||
                    !int.TryParse(row.HomeScore, NumberStyles.Integer, CultureInfo.InvariantCulture, out var homeScore) ||
                    !int.TryParse(row.AwayScore, NumberStyles.Integer, CultureInfo.InvariantCulture, out var awayScore))
                {
                    continue;
                }

                var homeId = TeamNameNormalizer.ToId(row.HomeTeam);
                var awayId = TeamNameNormalizer.ToId(row.AwayTeam);
                var resultId = CryptoUtil.GetSha256($"{homeId}-{awayId}-{date:O}-{row.Tournament}-{homeScore}-{awayScore}");

                if (!importedIds.Add(resultId))
                    continue;

                await CreateTeamIfMissing(row.HomeTeam, OloraculoDataFiles.HistoricalResultsCsv, ct);
                await CreateTeamIfMissing(row.AwayTeam, OloraculoDataFiles.HistoricalResultsCsv, ct);

                _db.Results.Add(new MatchResult
                {
                    Id = resultId,
                    HomeTeamId = homeId,
                    AwayTeamId = awayId,
                    HomeGoals = homeScore,
                    AwayGoals = awayScore,
                    Date = date,
                    Tournament = row.Tournament,
                    Neutral = bool.TryParse(row.Neutral, out var neutral) && neutral,
                    Source = OloraculoDataFiles.HistoricalResultsCsv
                });
            }
        }

        private async Task GenerateFixturesAsync(CancellationToken ct)
        {
            _db.Fixtures.RemoveRange(_db.Fixtures);
            var groups = await _db.Groups.AsNoTracking().ToListAsync(ct);

            foreach (var group in groups.OrderBy(g => g.Name))
            {
                var teams = group.TeamIds;
                for (var i = 0; i < teams.Count; i++)
                {
                    for (var j = i + 1; j < teams.Count; j++)
                    {
                        _db.Fixtures.Add(new Fixture
                        {
                            Id = Fixture.GenerateFixtureId(group.Name, teams[i], teams[j]),
                            Group = group.Name,
                            HomeTeamId = teams[i],
                            AwayTeamId = teams[j],
                            NeutralVenue = true,
                            Source = $"derived from {OloraculoDataFiles.GroupsCsv}"
                        });
                    }
                }
            }
        }

        private async Task CreateTeamIfMissing(string name, string sourceFile, CancellationToken ct)
        {
            var canonical = TeamNameNormalizer.CanonicalName(name);
            var id = TeamNameNormalizer.ToId(canonical);
            if (await _db.Teams.FindAsync([id], ct) is null)
                _db.Teams.Add(new Team { Id = id, Name = canonical, Source = sourceFile });
        }

        private async Task EnsureFixtureResultColumnsAsync(CancellationToken ct)
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                await connection.OpenAsync(ct);

            try
            {
                var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA table_info(\"Fixtures\")";
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                        columns.Add(reader.GetString(1));
                }

                if (!columns.Contains("HomeGoals"))
                    await ExecuteSchemaAsync("ALTER TABLE \"Fixtures\" ADD COLUMN \"HomeGoals\" INTEGER NULL", ct);
                if (!columns.Contains("AwayGoals"))
                    await ExecuteSchemaAsync("ALTER TABLE \"Fixtures\" ADD COLUMN \"AwayGoals\" INTEGER NULL", ct);
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }

            async Task ExecuteSchemaAsync(string sql, CancellationToken token)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                await command.ExecuteNonQueryAsync(token);
            }
        }

        private string FullPath(string fileName) => Path.Combine(_environment.ContentRootPath, "Data", fileName);
    }
}
