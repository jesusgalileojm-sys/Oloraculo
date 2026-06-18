using System.Net.Http.Json;
using Oloraculo.Web.Models;

namespace Oloraculo.Web.Services;

/// <summary>
/// Servicio para obtener datos en vivo de SofaScore y calcular probabilidades actualizadas
/// </summary>
public interface ILiveMatchService
{
    Task<SofascoreLiveMatch> GetLiveMatchAsync(int matchId);
    Task<LivePredictionContext> GetLiveContextAsync(int matchId, double baseGoalProbability);
    Task<GoalProbabilityUpdate> CalculateLiveGoalProbabilityAsync(LivePredictionContext context);
}

public class GoalProbabilityUpdate
{
    public double Next5Minutes { get; set; }
    public double Next15Minutes { get; set; }
    public double EndOfMatch { get; set; }
    public string Reasoning { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class LiveMatchService : ILiveMatchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LiveMatchService> _logger;
    private readonly string _sofascoreBaseUrl = "https://api.sofascore.com/api/v1";

    public LiveMatchService(HttpClient httpClient, ILogger<LiveMatchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Obtiene datos en vivo del partido desde SofaScore
    /// </summary>
    public async Task<SofascoreLiveMatch> GetLiveMatchAsync(int matchId)
    {
        try
        {
            var url = $"{_sofascoreBaseUrl}/event/{matchId}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadAsAsync<dynamic>();
            
            // Parsear respuesta de SofaScore
            var match = new SofascoreLiveMatch
            {
                Id = matchId,
                Status = data["event"]["status"]["type"]?.ToString() ?? "unknown",
                HomeTeam = new TeamInfo 
                { 
                    Id = data["event"]["homeTeam"]["id"],
                    Name = data["event"]["homeTeam"]["name"],
                    ShortName = data["event"]["homeTeam"]["shortName"]
                },
                AwayTeam = new TeamInfo 
                { 
                    Id = data["event"]["awayTeam"]["id"],
                    Name = data["event"]["awayTeam"]["name"],
                    ShortName = data["event"]["awayTeam"]["shortName"]
                },
                HomeScore = data["event"]["homeScore"]["current"] ?? 0,
                AwayScore = data["event"]["awayScore"]["current"] ?? 0,
                CurrentPeriod = data["event"]["status"]["period"] ?? 0,
                Minute = data["event"]["status"]["minute"] ?? 0,
            };

            _logger.LogInformation($"Match en vivo: {match.HomeTeam.Name} {match.HomeScore}-{match.AwayScore} {match.AwayTeam.Name}");
            return match;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error obteniendo datos de SofaScore: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Construye contexto con eventos y estadísticas para el cálculo
    /// </summary>
    public async Task<LivePredictionContext> GetLiveContextAsync(int matchId, double baseGoalProbability)
    {
        var liveMatch = await GetLiveMatchAsync(matchId);
        
        // Obtener eventos del partido
        var events = await GetMatchEventsAsync(matchId);
        liveMatch.Events = events;

        // Obtener estadísticas
        var stats = await GetMatchStatisticsAsync(matchId);
        liveMatch.Statistics = stats;

        // Agregadores
        var dangerousPlays = events.Count(e => 
            e.Type == "shot" || e.Type == "corner" || e.Type == "duel" || 
            (e.Shot?.IsOnTarget ?? false));
        
        var shotsOnTarget = events.Count(e => 
            e.Type == "shot" && e.Shot?.IsOnTarget == true);
        
        var corners = events.Count(e => e.Type == "corner");
        
        var avgxG = events
            .Where(e => e.Type == "shot" && e.Shot?.ExpectedGoals.HasValue == true)
            .Average(e => e.Shot.ExpectedGoals ?? 0);

        return new LivePredictionContext
        {
            LiveMatch = liveMatch,
            BaseGoalProbability = baseGoalProbability,
            DangerousPlays = dangerousPlays,
            ShotsOnTarget = shotsOnTarget,
            CornerCount = corners,
            AvgxG = double.IsNaN(avgxG) ? 0 : avgxG,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Calcula la probabilidad ACTUALIZADA de gol con datos en vivo
    /// Factores considerados: xG, tiros, córners, posesión, ritmo del partido
    /// </summary>
    public async Task<GoalProbabilityUpdate> CalculateLiveGoalProbabilityAsync(LivePredictionContext context)
    {
        var match = context.LiveMatch;
        var baseProb = context.BaseGoalProbability;
        
        // ====== FACTORES EN VIVO ======
        
        // 1. EXPECTED GOALS (xG) - el mejor predictor
        var xGFactor = Math.Min(context.AvgxG * 0.25, 0.15); // xG aumenta probabilidad
        
        // 2. RITMO: Tiros en los últimos N eventos
        var recentShots = match.Events
            .Where(e => e.Type == "shot" && e.Minute > (match.Minute - 5))
            .Count();
        var shotRythmFactor = recentShots * 0.05;
        
        // 3. OPORTUNIDADES CLARAS: Córners + Faltas desde área
        var cornerFactor = context.CornerCount * 0.03;
        
        // 4. CONTROL: Posesión (mayor posesión = más chances)
        var homeStats = match.Statistics?.HomeTeam;
        var possessionFactor = (homeStats?.Possession ?? 50) > 55 ? 0.05 : -0.02;
        
        // 5. MINUTO DEL PARTIDO: Partidos tienden a ser más abiertos en finales
        var minuteFactor = match.Minute > 70 ? 0.03 : (match.Minute < 15 ? -0.02 : 0);
        
        // 6. DENSIDAD: Si hay muchos eventos en corto tiempo = party abierto
        var eventDensity = context.DangerousPlays > 10 ? 0.04 : 0;
        
        // ====== CÁLCULO FINAL ======
        var adjustedProb = baseProb 
            + xGFactor 
            + shotRythmFactor 
            + cornerFactor 
            + possessionFactor 
            + minuteFactor 
            + eventDensity;
        
        // Clampear entre 0 y 0.95 (nunca 100%)
        adjustedProb = Math.Max(0, Math.Min(adjustedProb, 0.95));
        
        // Calcular para diferentes horizontes temporales
        var timeRemaining = Math.Max(0, 90 - match.Minute);
        
        return new GoalProbabilityUpdate
        {
            Next5Minutes = Math.Min(adjustedProb * 0.6, 0.95), // Más corto, menos probable
            Next15Minutes = Math.Min(adjustedProb * 1.2, 0.95),
            EndOfMatch = Math.Min(adjustedProb * (timeRemaining / 90.0), 0.95),
            Reasoning = GenerateReasoning(context, new Dictionary<string, double>
            {
                { "xG", xGFactor },
                { "ShotRhythm", shotRythmFactor },
                { "Corners", cornerFactor },
                { "Possession", possessionFactor },
                { "Minute", minuteFactor },
                { "EventDensity", eventDensity }
            }),
            UpdatedAt = DateTime.UtcNow
        };
    }

    private async Task<List<SofascoreMatchEvent>> GetMatchEventsAsync(int matchId)
    {
        try
        {
            var url = $"{_sofascoreBaseUrl}/event/{matchId}/play-by-play";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Aquí parsearías la respuesta de SofaScore
            // Por ahora retornamos lista vacía - ajusta según el formato real
            return new List<SofascoreMatchEvent>();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error obteniendo eventos: {ex.Message}");
            return new List<SofascoreMatchEvent>();
        }
    }

    private async Task<MatchStatistics> GetMatchStatisticsAsync(int matchId)
    {
        try
        {
            var url = $"{_sofascoreBaseUrl}/event/{matchId}/statistics";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // Parsear estadísticas de SofaScore
            return new MatchStatistics
            {
                HomeTeam = new TeamStats(),
                AwayTeam = new TeamStats()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error obteniendo estadísticas: {ex.Message}");
            return new MatchStatistics { HomeTeam = new TeamStats(), AwayTeam = new TeamStats() };
        }
    }

    private string GenerateReasoning(LivePredictionContext context, Dictionary<string, double> factors)
    {
        var reasoning = $"**Análisis en vivo ({context.LiveMatch.Minute}') - {context.LiveMatch.HomeTeam.ShortName} vs {context.LiveMatch.AwayTeam.ShortName}**\n\n";
        reasoning += $"📊 **Factores:**\n";
        reasoning += $"• xG promedio: {context.AvgxG:F2}\n";
        reasoning += $"• Tiros al arco: {context.ShotsOnTarget}\n";
        reasoning += $"• Córners: {context.CornerCount}\n";
        reasoning += $"• Jugadas peligrosas: {context.DangerousPlays}\n\n";
        
        reasoning += "⚡ **Influencia de factores:**\n";
        foreach (var factor in factors.OrderByDescending(f => Math.Abs(f.Value)))
        {
            var emoji = factor.Value > 0 ? "📈" : "📉";
            reasoning += $"{emoji} {factor.Key}: {factor.Value:+0.00;-0.00;0.00}\n";
        }
        
        return reasoning;
    }
}
