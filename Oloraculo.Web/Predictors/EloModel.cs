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
                    Explanation = "Faltan ratings Elo para uno o ambos equipos.",
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
                Explanation = $"Eloquehay: basado en Elo {context.HomeElo.Value} para {context.HomeTeam.Name} " +
                $"y {context.AwayElo.Value} para {context.AwayTeam.Name}.",
                Drivers = new[] { $"Diferencia Elo: {Diff:+0;-0}" },
                FeaturesUsed = new[] { "Elo del equipo A", "Elo del equipo B" },
                FeaturesMissing = Array.Empty<string>(),
                Sources = new[] { SourceMetadata.EloRatings },
                Degraded = false
            };
        }
    }
}
