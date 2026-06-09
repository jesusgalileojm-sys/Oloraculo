using Oloraculo.Web.Models;
using Oloraculo.Web.Probability;

namespace Oloraculo.Web.Predictors
{
    public class EloModel : IPredictor
    {
        public string Name => "Elo";
        public int Priority => 2;

        public MatchPrediction Predict(MatchContext context)
        {
            if (context.HomeElo is null || context.AwayElo is null)
            {
                return new MatchPrediction
                {
                    PredictorName = Name,
                    PredictorPriority = Priority,
                    FixtureId = context.Fixture.Id,
                    HomeTeamId = context.HomeTeam.Id,
                    AwayTeamId = context.AwayTeam.Id,
                    Outcome = OutcomeProbabilities.Uniform,
                    Explanation = "Elo ratings are not available for both teams.",
                    Degraded = true
                };
            }

            double Expected = ProbabilityHelper.EloExpectation(context.HomeElo.Value, context.AwayElo.Value);
            double Diff = context.HomeElo.Value - context.AwayElo.Value;
            OutcomeProbabilities Outcome = ProbabilityHelper.OutcomeFromExpectation(Expected, Diff);
            return new MatchPrediction
            {
                PredictorName = Name,
                PredictorPriority = Priority,
                FixtureId = context.Fixture.Id,
                HomeTeamId = context.HomeTeam.Id,
                AwayTeamId = context.AwayTeam.Id,
                Outcome = Outcome,
                Explanation = $"Eloquehay - Based on Elo ratings of {context.HomeElo.Value} for {context.HomeTeam.Name} " +
                $"and {context.AwayElo.Value} for {context.AwayTeam.Name}.",
                Drivers = new[] { $"Elo gap: {Diff:+0;-0}" },
                FeaturesUsed = new[] { "Home Team Elo", "Away Team Elo" },
                FeaturesMissing = Array.Empty<string>(),
                Sources = new[] { SourceMetadata.EloRatings },
                Degraded = false
            };
        }
    }
}
