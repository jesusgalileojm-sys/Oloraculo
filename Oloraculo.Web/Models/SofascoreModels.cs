using System.Text.Json.Serialization;

namespace Oloraculo.Web.Models;

/// <summary>
/// Modelos para consumir datos en vivo de SofaScore API
/// Documentación: https://www.sofascore.com/
/// </summary>

public class SofascoreMatchEvent
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("eventId")]
    public int EventId { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } // "pass", "shot", "corner", "foul", "tackle", "dribble", "duel"

    [JsonPropertyName("period")]
    public int Period { get; set; } // 1 = 1st half, 2 = 2nd half, etc.

    [JsonPropertyName("minute")]
    public int Minute { get; set; }

    [JsonPropertyName("second")]
    public int Second { get; set; }

    [JsonPropertyName("teamId")]
    public int TeamId { get; set; }

    [JsonPropertyName("playerId")]
    public int? PlayerId { get; set; }

    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } // ej: "Assist", "Goal", "Yellow Card"

    [JsonPropertyName("isHome")]
    public bool IsHome { get; set; }

    // Eventos específicos
    [JsonPropertyName("shot")]
    public ShotDetail Shot { get; set; }

    [JsonPropertyName("pass")]
    public PassDetail Pass { get; set; }

    [JsonPropertyName("duel")]
    public DuelDetail Duel { get; set; }
}

public class ShotDetail
{
    [JsonPropertyName("xG")]
    public double? ExpectedGoals { get; set; } // Probabilidad de gol (0-1)

    [JsonPropertyName("isOnTarget")]
    public bool IsOnTarget { get; set; }

    [JsonPropertyName("blockedX")]
    public double? BlockedX { get; set; }

    [JsonPropertyName("blockedY")]
    public double? BlockedY { get; set; }

    [JsonPropertyName("resultType")]
    public string ResultType { get; set; } // "Goal", "Saved", "Blocked", "Miss", "Post"
}

public class PassDetail
{
    [JsonPropertyName("length")]
    public double Length { get; set; }

    [JsonPropertyName("angle")]
    public double Angle { get; set; }

    [JsonPropertyName("accurate")]
    public bool Accurate { get; set; }

    [JsonPropertyName("shortPass")]
    public bool ShortPass { get; set; }

    [JsonPropertyName("longBall")]
    public bool LongBall { get; set; }

    [JsonPropertyName("chanceCreated")]
    public bool ChanceCreated { get; set; }
}

public class DuelDetail
{
    [JsonPropertyName("won")]
    public bool Won { get; set; }

    [JsonPropertyName("type")]
    public string DuelType { get; set; } // "aerial", "ground", "tackle"
}

public class SofascoreLiveMatch
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } // "notStarted", "inProgress", "finished"

    [JsonPropertyName("homeTeam")]
    public TeamInfo HomeTeam { get; set; }

    [JsonPropertyName("awayTeam")]
    public TeamInfo AwayTeam { get; set; }

    [JsonPropertyName("homeScore")]
    public int HomeScore { get; set; }

    [JsonPropertyName("awayScore")]
    public int AwayScore { get; set; }

    [JsonPropertyName("currentPeriod")]
    public int CurrentPeriod { get; set; }

    [JsonPropertyName("minute")]
    public int Minute { get; set; }

    [JsonPropertyName("events")]
    public List<SofascoreMatchEvent> Events { get; set; } = new();

    [JsonPropertyName("statistics")]
    public MatchStatistics Statistics { get; set; }
}

public class TeamInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("shortName")]
    public string ShortName { get; set; }
}

public class MatchStatistics
{
    [JsonPropertyName("homeTeam")]
    public TeamStats HomeTeam { get; set; }

    [JsonPropertyName("awayTeam")]
    public TeamStats AwayTeam { get; set; }
}

public class TeamStats
{
    [JsonPropertyName("totalShots")]
    public int TotalShots { get; set; }

    [JsonPropertyName("shotsOnTarget")]
    public int ShotsOnTarget { get; set; }

    [JsonPropertyName("possession")]
    public double Possession { get; set; }

    [JsonPropertyName("passes")]
    public int Passes { get; set; }

    [JsonPropertyName("passAccuracy")]
    public double PassAccuracy { get; set; }

    [JsonPropertyName("corners")]
    public int Corners { get; set; }

    [JsonPropertyName("fouls")]
    public int Fouls { get; set; }

    [JsonPropertyName("tacklles")]
    public int Tackles { get; set; }

    [JsonPropertyName("dribbles")]
    public int Dribbles { get; set; }

    [JsonPropertyName("expectedGoals")]
    public double ExpectedGoals { get; set; } // xG total
}

/// <summary>
/// Contexto para cálculos de probabilidad con datos en vivo
/// </summary>
public class LivePredictionContext
{
    public SofascoreLiveMatch LiveMatch { get; set; }
    public double BaseGoalProbability { get; set; } // Predicción pre-partido
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Agregadores de eventos
    public int DangerousPlays { get; set; }
    public int ShotsOnTarget { get; set; }
    public int CornerCount { get; set; }
    public double AvgxG { get; set; }
}
