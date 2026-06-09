using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Oloraculo.Web;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Helpers;
using Oloraculo.Web.Models;
using Oloraculo.Web.Models.CsvModels;
using Oloraculo.Web.Predictors;
using Oloraculo.Web.Probability;
using Oloraculo.Web.Services;
using Oloraculo.Web.Services.Simulation;
using System.Globalization;

namespace Oloraculo.Web.Tests;

public class CoreTests
{
    [Fact]
    public void OutcomeProbabilities_NormalizesAndUsesOutcomeLabels()
    {
        var p = new OutcomeProbabilities(2, 1, 1).Normalize();

        Assert.True(p.IsValid);
        Assert.Equal(0.5, p.HomeWin, 3);
        Assert.Equal("Home", p.TopPick);
    }

    [Theory]
    [InlineData("Korea Republic", "south-korea")]
    [InlineData("Türkiye", "turkey")]
    [InlineData("USA", "united-states")]
    public void TeamNameNormalizer_HandlesAliases(string input, string expected)
    {
        Assert.Equal(expected, TeamNameNormalizer.ToId(input));
    }

    [Fact]
    public void OutcomeFromExpectation_TreatsEqualMagnitudeGapsSymmetrically()
    {
        var strongerHome = ProbabilityHelper.OutcomeFromExpectation(.78, 400);
        var strongerAway = ProbabilityHelper.OutcomeFromExpectation(.22, -400);

        Assert.Equal(strongerHome.Draw, strongerAway.Draw, 6);
    }

    [Fact]
    public void PoissonScoreline_ProducesARealProbabilityGrid()
    {
        var dist = ProbabilityHelper.PoissonScoreline(2.2, .7);
        var sum = 0.0;
        for (var h = 0; h <= dist.MaxGoals; h++)
            for (var a = 0; a <= dist.MaxGoals; a++)
                sum += dist.Probability(h, a);

        Assert.Equal(1.0, sum, 6);
        Assert.True(dist.ToOutcome().HomeWin > dist.ToOutcome().AwayWin);
        Assert.NotEqual((0, 0), dist.MostLikelyScoreline());
    }

    [Fact]
    public void GoalModel_ProducesUsableScorelineWhenTeamsHaveEnoughHistory()
    {
        var model = new GoalModel(
        [
            Result("a", "b", 2, 0),
            Result("a", "b", 1, 0),
            Result("b", "a", 1, 2)
        ]);

        var prediction = model.Predict(TestContext());

        Assert.False(prediction.Degraded);
        Assert.NotNull(prediction.Scoreline);
        Assert.True(prediction.ExpectedHomeGoals > 0.1);
        Assert.True(prediction.Outcome.IsValid);
    }

    [Fact]
    public void ContextModel_DoesNotClaimLineupsOrOddsWereUsedWithoutConversionLogic()
    {
        var goal = new GoalModel(
        [
            Result("a", "b", 2, 0),
            Result("a", "b", 1, 0),
            Result("b", "a", 1, 2)
        ]);
        var context = TestContext(fixtureContext: new FixtureContext
        {
            FixtureId = "test",
            HasLineups = true,
            HasOdds = true
        });

        var prediction = new GoalPlusRecentContextModel(goal).Predict(context);

        Assert.DoesNotContain(nameof(FeaturesEnum.Lineups), prediction.FeaturesUsed);
        Assert.DoesNotContain(nameof(FeaturesEnum.Odds), prediction.FeaturesUsed);
        Assert.Contains("lineup impact model", prediction.FeaturesMissing);
        Assert.Contains("odds calibration", prediction.FeaturesMissing);
        Assert.True(prediction.Degraded);
    }

    [Fact]
    public void ContextModel_BecomesUsableWhenAvailabilityActuallyAdjustsGoals()
    {
        var goal = new GoalModel(
        [
            Result("a", "b", 2, 0),
            Result("a", "b", 1, 0),
            Result("b", "a", 1, 2)
        ]);
        var context = TestContext(fixtureContext: new FixtureContext
        {
            FixtureId = "test",
            UnavailableHomePlayers = 2
        });

        var prediction = new GoalPlusRecentContextModel(goal).Predict(context);

        Assert.False(prediction.Degraded);
        Assert.Contains(nameof(FeaturesEnum.PlayerAvailability), prediction.FeaturesUsed);
    }

    [Fact]
    public void FinalSelector_ChoosesHighestUsableRungWithoutAveraging()
    {
        var form = Prediction(3, "Recent Form", .05, .05, .90);
        var goal = Prediction(4, "Goal", .90, .05, .05, scoreline: ProbabilityHelper.PoissonScoreline(3.0, .4));
        var context = Prediction(5, "Context", .10, .80, .10, degraded: true, missing: ["availability"]);

        var final = FinalPredictionSelector.Select([form, goal, context]);

        Assert.Equal("Final Oracle", final.PredictorName);
        Assert.Equal(4, final.PredictorPriority);
        Assert.Equal(goal.Outcome, final.Outcome);
        Assert.NotEqual(.475, final.Outcome.HomeWin, 3);
    }

    [Fact]
    public async Task CsvImport_CreatesTeamsGroupsFixturesRatingsAndResults()
    {
        await using var db = await NewDb();
        var importer = new CsvImportService(db, new TestEnvironment(WebProjectRoot()));

        var report = await importer.ImportAllAsync();

        Assert.True(report.Teams >= 48);
        Assert.Equal(12, report.Groups);
        Assert.Equal(72, report.Fixtures);
        Assert.True(report.Ratings > 0);
        Assert.True(report.Results > 0);
        Assert.Equal(ExpectedUniqueHistoricalResultIds(), report.Results);
        Assert.DoesNotContain(await db.Fixtures.ToListAsync(), f => string.IsNullOrWhiteSpace(f.Group));
    }

    [Fact]
    public async Task Evaluation_StoresFixtureLevelKnownResult()
    {
        await using var db = await NewDb();
        var fixture = new Fixture { Id = "f1", Group = "A", HomeTeamId = "a", AwayTeamId = "b" };
        db.Teams.AddRange(new Team { Id = "a", Name = "A" }, new Team { Id = "b", Name = "B" });
        db.Fixtures.Add(fixture);
        db.Snapshots.Add(new PredictionSnapshot
        {
            Kind = "match",
            FixtureId = "f1",
            ModelName = "Final Oracle",
            InputSummaryHash = "hash",
            PayloadJson = "{}",
            Explanation = "test",
            HomeWin = .6,
            Draw = .2,
            AwayWin = .2
        });
        await db.SaveChangesAsync();

        var count = await new EvaluationService(db).EvaluateLatestSnapshotAsync(fixture, 2, 1);

        Assert.Equal(1, count);
        Assert.True(fixture.IsPlayed);
        Assert.Equal(2, fixture.HomeGoals);
        Assert.Equal(1, fixture.AwayGoals);
    }

    [Fact]
    public async Task SnapshotService_SavesTournamentSnapshotAgainstLegacyNonNullProbabilityColumns()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE "Snapshots" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Snapshots" PRIMARY KEY AUTOINCREMENT,
                    "Kind" TEXT NOT NULL,
                    "FixtureId" TEXT NULL,
                    "ModelName" TEXT NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "InputSummaryHash" TEXT NOT NULL,
                    "PayloadJson" TEXT NOT NULL,
                    "Explanation" TEXT NOT NULL,
                    "HomeWin" REAL NOT NULL,
                    "Draw" REAL NOT NULL,
                    "AwayWin" REAL NOT NULL
                );
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<OloraculoDbContext>().UseSqlite(connection).Options;
        await using var db = new OloraculoDbContext(options);

        var snapshot = await new SnapshotService(db).SaveTournamentAsync(new TournamentProjection
        {
            ModelName = "Final",
            InputSummaryHash = "hash",
            Simulations = 1,
            Teams = []
        });

        Assert.Equal("tournament", snapshot.Kind);
        Assert.Equal(0, snapshot.AwayWin);
    }

    [Fact]
    public async Task Simulation_IsRepeatableWithSameSeed()
    {
        await using var db = await ImportedDb();
        var service = Simulation(db, simulations: 3, seed: 42);

        var one = await service.RunAsync(saveSnapshot: false);
        var two = await service.RunAsync(saveSnapshot: false);

        Assert.Equal(one.Teams.Select(t => t.WinTournament), two.Teams.Select(t => t.WinTournament));
        Assert.Equal(1.0, one.Teams.Sum(t => t.WinTournament), 6);
    }

    [Fact]
    public async Task Simulation_UsesKnownGroupFixtureScores()
    {
        await using var db = await ImportedDb();
        var mexicoFixtures = await db.Fixtures
            .Where(f => f.Group == "A" && (f.HomeTeamId == "mexico" || f.AwayTeamId == "mexico"))
            .ToListAsync();

        foreach (var fixture in mexicoFixtures)
        {
            fixture.IsPlayed = true;
            fixture.HomeGoals = fixture.HomeTeamId == "mexico" ? 10 : 0;
            fixture.AwayGoals = fixture.AwayTeamId == "mexico" ? 10 : 0;
        }
        await db.SaveChangesAsync();

        var projection = await Simulation(db, simulations: 5, seed: 7).RunAsync(saveSnapshot: false);
        var mexico = projection.Teams.Single(t => t.TeamId == "mexico");

        Assert.Equal(1.0, mexico.WinGroup, 6);
        Assert.Equal(1.0, mexico.Qualify, 6);
    }

    private static SimulationService Simulation(OloraculoDbContext db, int simulations, int seed)
    {
        var options = Options.Create(new OloraculoConfig
        {
            GoalModelYearsWindow = 3,
            RecentResultCount = 8,
            SimulationCount = simulations,
            SimulationSeed = seed
        });
        var prediction = new PredictionService(db, options);
        var snapshots = new SnapshotService(db);
        return new SimulationService(db, prediction, snapshots, options);
    }

    private static async Task<OloraculoDbContext> ImportedDb()
    {
        var db = await NewDb();
        await new CsvImportService(db, new TestEnvironment(WebProjectRoot())).ImportAllAsync();
        return db;
    }

    private static async Task<OloraculoDbContext> NewDb()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OloraculoDbContext>().UseSqlite(connection).Options;
        var db = new OloraculoDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static MatchContext TestContext(string homeId = "a", string awayId = "b", FixtureContext? fixtureContext = null) => new()
    {
        Fixture = new Fixture { Id = "test", HomeTeamId = homeId, AwayTeamId = awayId, NeutralVenue = true },
        HomeTeam = new Team { Id = homeId, Name = homeId.ToUpperInvariant() },
        AwayTeam = new Team { Id = awayId, Name = awayId.ToUpperInvariant() },
        HomeElo = new Rating { TeamId = homeId, Type = RatingTypeEnum.Elo, Value = 1800, Source = "test" },
        AwayElo = new Rating { TeamId = awayId, Type = RatingTypeEnum.Elo, Value = 1700, Source = "test" },
        HomeRecentMatchHistory = [],
        AwayRecentMatchHistory = [],
        FixtureContext = fixtureContext
    };

    private static MatchResult Result(string home, string away, int homeGoals, int awayGoals) => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        HomeTeamId = home,
        AwayTeamId = away,
        HomeGoals = homeGoals,
        AwayGoals = awayGoals,
        Date = DateTimeOffset.UtcNow.AddDays(-Random.Shared.Next(1, 30)),
        Tournament = "test",
        Neutral = true,
        Source = "test"
    };

    private static MatchPrediction Prediction(
        int priority,
        string name,
        double home,
        double draw,
        double away,
        bool degraded = false,
        IReadOnlyList<string>? missing = null,
        ScorelineDistribution? scoreline = null) => new()
    {
        PredictorPriority = priority,
        PredictorName = name,
        FixtureId = "f",
        HomeTeamId = "a",
        AwayTeamId = "b",
        Outcome = new OutcomeProbabilities(home, draw, away).Normalize(),
        Scoreline = scoreline,
        Explanation = name,
        FeaturesMissing = missing ?? [],
        Degraded = degraded
    };

    private static string WebProjectRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Oloraculo.Web"));

    private static int ExpectedUniqueHistoricalResultIds()
    {
        var rows = CsvParsingHelper.ReadCsv<HistoricalResultCsvRow>(Path.Combine(WebProjectRoot(), "Data", "historical_results.csv"));
        var ids = new HashSet<string>(StringComparer.Ordinal);

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
            ids.Add(CryptoUtil.GetSha256($"{homeId}-{awayId}-{date:O}-{row.Tournament}-{homeScore}-{awayScore}"));
        }

        return ids.Count;
    }

    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Oloraculo.Web.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
