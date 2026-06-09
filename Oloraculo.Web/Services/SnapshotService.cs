using Oloraculo.Web.DAL;
using Oloraculo.Web.Helpers;
using Oloraculo.Web.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oloraculo.Web.Services
{
    public class SnapshotService
    {
        private readonly OloraculoDbContext _db;
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

        public SnapshotService(OloraculoDbContext db) => _db = db;

        public async Task<PredictionSnapshot> SaveMatchAsync(MatchPrediction prediction, CancellationToken ct = default)
        {
            var payload = JsonSerializer.Serialize(new
            {
                prediction.PredictorName,
                Outcome = prediction.Outcome,
                prediction.ExpectedHomeGoals,
                prediction.ExpectedAwayGoals,
                MostLikelyScore = prediction.MostLikelyScore is null ? null : $"{prediction.MostLikelyScore.Value.Home}-{prediction.MostLikelyScore.Value.Away}",
                prediction.FeaturesUsed,
                prediction.FeaturesMissing,
                Sources = prediction.Sources.Select(s => s.ToString())
            }, JsonOptions);

            var snapshot = new PredictionSnapshot
            {
                Kind = "match",
                FixtureId = prediction.FixtureId,
                ModelName = prediction.PredictorName,
                CreatedAt = DateTimeOffset.UtcNow,
                InputSummaryHash = CryptoUtil.GetSha256($"{prediction.FixtureId}|{DateTimeOffset.UtcNow:yyyyMMddHH}"),
                PayloadJson = payload,
                Explanation = prediction.Explanation,
                HomeWin = prediction.Outcome.HomeWin,
                Draw = prediction.Outcome.Draw,
                AwayWin = prediction.Outcome.AwayWin
            };
            _db.Snapshots.Add(snapshot);
            await _db.SaveChangesAsync(ct);
            return snapshot;
        }

        public async Task<PredictionSnapshot> SaveTournamentAsync(TournamentProjection projection, CancellationToken ct = default)
        {
            var payload = JsonSerializer.Serialize(projection, JsonOptions);
            var snapshot = new PredictionSnapshot
            {
                Kind = "tournament",
                ModelName = projection.ModelName,
                CreatedAt = projection.GeneratedAt,
                InputSummaryHash = projection.InputSummaryHash,
                PayloadJson = payload,
                Explanation = $"{projection.Simulations:N0} simulaciones usando {projection.ModelName}.",
                HomeWin = 0,
                Draw = 0,
                AwayWin = 0
            };
            _db.Snapshots.Add(snapshot);
            await _db.SaveChangesAsync(ct);
            return snapshot;
        }
    }
}
