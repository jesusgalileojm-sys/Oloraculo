using Oloraculo.Web.Models;
using Oloraculo.Web.Probability;

namespace Oloraculo.Web.Predictors
{
    public static class FinalPredictionSelector
    {
        public static MatchPrediction Select(IReadOnlyList<MatchPrediction> ladder)
        {
            if (ladder.Count == 0)
                return EmptyFinal();

            var ordered = ladder.OrderBy(p => p.PredictorPriority).ToList();
            var selected = ordered.LastOrDefault(p => !p.Degraded) ?? ordered.First();
            var skippedHigher = ordered
                .Where(p => p.PredictorPriority > selected.PredictorPriority && p.Degraded)
                .OrderByDescending(p => p.PredictorPriority)
                .ToList();

            var drivers = new List<string>
            {
                $"Selected {selected.PredictorName} as the highest usable rung."
            };
            drivers.AddRange(skippedHigher.Select(p => $"Skipped {p.PredictorName}: {Reason(p)}"));
            drivers.AddRange(selected.Drivers);

            return new MatchPrediction
            {
                PredictorName = "Final Oracle",
                PredictorPriority = selected.PredictorPriority,
                FixtureId = selected.FixtureId,
                HomeTeamId = selected.HomeTeamId,
                AwayTeamId = selected.AwayTeamId,
                Outcome = selected.Outcome,
                ExpectedHomeGoals = selected.ExpectedHomeGoals,
                ExpectedAwayGoals = selected.ExpectedAwayGoals,
                Scoreline = selected.Scoreline,
                MostLikelyScore = selected.MostLikelyScore,
                Explanation = BuildExplanation(selected, skippedHigher),
                Drivers = drivers,
                FeaturesUsed = selected.FeaturesUsed,
                FeaturesMissing = selected.FeaturesMissing,
                Sources = selected.Sources.Concat([new SourceMetadata("model ladder", "derived", Notes: selected.PredictorName)]).ToList(),
                Degraded = selected.Degraded
            };
        }

        private static string BuildExplanation(MatchPrediction selected, IReadOnlyList<MatchPrediction> skippedHigher)
        {
            if (skippedHigher.Count == 0)
                return $"Final Oracle selected {selected.PredictorName}, the highest usable rung. {selected.Explanation}";

            var skipped = string.Join("; ", skippedHigher.Select(p => $"{p.PredictorName} {Reason(p)}"));
            return $"Final Oracle selected {selected.PredictorName} because {skipped}. {selected.Explanation}";
        }

        private static string Reason(MatchPrediction prediction)
        {
            if (prediction.FeaturesMissing.Count == 0)
                return "was degraded";

            return $"was degraded: missing {string.Join(", ", prediction.FeaturesMissing)}";
        }

        private static MatchPrediction EmptyFinal() => new()
        {
            PredictorName = "Final Oracle",
            PredictorPriority = 0,
            Outcome = OutcomeProbabilities.Uniform,
            Explanation = "Final Oracle had no ladder predictions, so it returned the baseline.",
            Drivers = ["No ladder predictions were available."],
            FeaturesMissing = ["ladder predictions"],
            Sources = [new SourceMetadata("model ladder", "derived")],
            Degraded = true
        };
    }
}
