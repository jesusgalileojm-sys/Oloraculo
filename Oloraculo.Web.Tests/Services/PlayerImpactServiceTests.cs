using Microsoft.Extensions.Options;
using Oloraculo.Web;
using Oloraculo.Web.Models;
using Oloraculo.Web.Models.ApiFootballModels;
using Oloraculo.Web.Models.CsvModels;
using Oloraculo.Web.Services;

namespace Oloraculo.Web.Tests;

public class PlayerImpactServiceTests : TestFixtures
{
    [Theory]
    [InlineData("Goalkeeper", PlayerPositions.Goalkeeper)]
    [InlineData("Defender", PlayerPositions.Defender)]
    [InlineData("Midfielder", PlayerPositions.Midfielder)]
    [InlineData("Attacker", PlayerPositions.Attacker)]
    [InlineData("Forward", PlayerPositions.Attacker)]
    [InlineData("Striker", PlayerPositions.Unknown)]
    public void NormalizePosition_MapsKnownRolesToPersistedValues(string input, string expected)
    {
        Assert.Equal(expected, AvailabilityNewsService.NormalizePosition(input));
    }

    [Fact]
    public void PlayerImpactSources_IdentifiesFallbackAndCombinesEnrichedSources()
    {
        Assert.True(PlayerImpactSources.IsFallback(PlayerImpactSources.Position));
        Assert.False(PlayerImpactSources.IsFallback(PlayerImpactSources.ApiStats));
        Assert.Equal(PlayerImpactSources.Position, PlayerImpactSources.Combine(null, PlayerImpactSources.Position));
        Assert.Equal(
            $"{PlayerImpactSources.Goalscorers}+{PlayerImpactSources.ApiStats}",
            PlayerImpactSources.Combine(PlayerImpactSources.Goalscorers, PlayerImpactSources.ApiStats, PlayerImpactSources.ApiStats));
    }

    [Fact]
    public void GoalscorerIndex_FiltersOwnGoalsDiscountsPenaltiesAndUsesLookback()
    {
        var rows = new[]
        {
            Goalscorer("2026-01-15", "France", "Kylian Mbappé", ownGoal: false, penalty: false),
            Goalscorer("2026-02-15", "France", "Kylian Mbappe", ownGoal: false, penalty: true),
            Goalscorer("2026-03-15", "France", "Kylian Mbappe", ownGoal: true, penalty: false),
            Goalscorer("2010-01-15", "France", "Kylian Mbappe", ownGoal: false, penalty: false),
            Goalscorer("2026-01-15", "Argentina", "Lionel Messi", ownGoal: false, penalty: false)
        };

        var index = PlayerImpactService.BuildGoalscorerIndex(rows, new DateOnly(2026, 6, 1), lookbackYears: 6);

        Assert.Equal(1.4, index["france|kylian-mbappe"], 3);
        Assert.Equal(1.0, index["argentina|lionel-messi"], 3);
    }

    [Fact]
    public async Task CalculateAsync_GoalscorerBoostsAttackerAndUnknownScorer()
    {
        var root = NewTempRoot();
        Directory.CreateDirectory(Path.Combine(root, "Data"));
        var recentDate = DateTime.UtcNow.AddMonths(-1).ToString("yyyy-MM-dd");
        await File.WriteAllTextAsync(
            Path.Combine(root, "Data", OloraculoDataFiles.GoalscorersCsv),
            $"""
            date,home_team,away_team,team,scorer,minute,own_goal,penalty
            {recentDate},France,Argentina,France,Kylian Mbappé,12,FALSE,FALSE
            {recentDate},France,Argentina,France,Kylian Mbappé,44,FALSE,FALSE
            {recentDate},France,Argentina,France,Unknown Scorer,80,FALSE,FALSE
            """);
        var service = ImpactService(root, goalscorersRawUrl: "");

        var known = await service.CalculateAsync("france", "Kylian Mbappe", AvailabilityNewsService.NormalizePlayerKey("Kylian Mbappe"), PlayerPositions.Attacker);
        var generic = await service.CalculateAsync("france", "Generic Attacker", AvailabilityNewsService.NormalizePlayerKey("Generic Attacker"), PlayerPositions.Attacker);
        var unknownScorer = await service.CalculateAsync("france", "Unknown Scorer", AvailabilityNewsService.NormalizePlayerKey("Unknown Scorer"), PlayerPositions.Unknown);
        var unknownFallback = await service.CalculateAsync("france", "Unknown Player", AvailabilityNewsService.NormalizePlayerKey("Unknown Player"), PlayerPositions.Unknown);

        Assert.True(known.Attack > generic.Attack);
        Assert.True(unknownScorer.Attack > unknownFallback.Attack);
        Assert.Equal(PlayerImpactSources.Goalscorers, known.Source);
    }

    [Fact]
    public async Task CalculateAsync_ApiStatsBoostAttackAndRegularDefensiveRoles()
    {
        var service = ImpactService(NewTempRoot(), goalscorersRawUrl: "");
        var attackerStats = new[]
        {
            new ApiPlayerStatistic
            {
                Games = new ApiPlayerGames { Position = PlayerPositions.Attacker, Lineups = 4, Minutes = 360, Rating = 7.1 },
                Goals = new ApiPlayerGoals { Total = 3, Assists = 1 }
            }
        };
        var defenderStats = new[]
        {
            new ApiPlayerStatistic
            {
                Games = new ApiPlayerGames { Position = PlayerPositions.Defender, Lineups = 4, Minutes = 360, Rating = 6.9 },
                Goals = new ApiPlayerGoals { Total = 0, Assists = 0 }
            }
        };

        var attacker = await service.CalculateAsync("france", "Kylian Mbappe", "kylian-mbappe", PlayerPositions.Attacker, attackerStats);
        var defender = await service.CalculateAsync("france", "William Saliba", "william-saliba", PlayerPositions.Defender, defenderStats);

        Assert.True(attacker.Attack > AvailabilityNewsService.ImpactForPosition(PlayerPositions.Attacker).Attack);
        Assert.True(defender.Defense > AvailabilityNewsService.ImpactForPosition(PlayerPositions.Defender).Defense);
        Assert.Equal(3, attacker.ApiGoals);
        Assert.Contains(PlayerImpactSources.ApiStats, defender.Source);
    }

    [Fact]
    public async Task CalculateAsync_MissingGoalscorerCacheFallsBackToPositionImpact()
    {
        var service = ImpactService(NewTempRoot(), goalscorersRawUrl: "");

        var impact = await service.CalculateAsync("france", "Generic Attacker", "generic-attacker", PlayerPositions.Attacker);

        Assert.Equal(AvailabilityNewsService.ImpactForPosition(PlayerPositions.Attacker).Attack, impact.Attack);
        Assert.Equal(AvailabilityNewsService.ImpactForPosition(PlayerPositions.Attacker).Defense, impact.Defense);
        Assert.Equal(PlayerImpactSources.Position, impact.Source);
    }

    private static PlayerImpactService ImpactService(string root, string goalscorersRawUrl) =>
        new(
            new HttpClient(new FakeHttpMessageHandler(new Dictionary<string, string>())),
            new TestEnvironment(root),
            Options.Create(new OloraculoConfig
            {
                GoalscorersRawUrl = goalscorersRawUrl,
                GoalscorerLookbackYears = 6
            }));

    private static GoalscorerCsvRow Goalscorer(string date, string team, string scorer, bool ownGoal, bool penalty) => new()
    {
        Date = date,
        Team = team,
        Scorer = scorer,
        OwnGoal = ownGoal.ToString(),
        Penalty = penalty.ToString()
    };
}
