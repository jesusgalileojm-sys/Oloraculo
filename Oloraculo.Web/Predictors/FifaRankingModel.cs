using Oloraculo.Web.Models;
using Oloraculo.Web.Probability;
using System.Threading.Tasks;

namespace Oloraculo.Web.Predictors
{
    public class FifaRankingModel : IPredictor
    {
        public string Name => "Ranking FIFA";
        public int Priority => 1;

        public MatchPrediction Predict(MatchContext context)
        {
            Rating? home = context.HomeFifaRank;
            Rating? away = context.AwayFifaRank;
            if (home is null || away is null)
            {
                return new MatchPrediction
                {
                    PredictorName = Name,
                    PredictorPriority = Priority,
                    FixtureId = context.Fixture.Id,
                    HomeTeamId = context.HomeTeam.Id,
                    AwayTeamId = context.AwayTeam.Id,
                    Outcome = OutcomeProbabilities.Uniform,
                    Explanation = "Faltan datos de ranking FIFA para uno o ambos equipos.",
                    Degraded = true
                };
            }

            double Diff = home.Value - away.Value;
            double Expected = ProbabilityHelper.EloExpectation(home.Value, away.Value);
            OutcomeProbabilities OutcomeProbability = ProbabilityHelper.OutcomeFromExpectation(Expected, Diff);
            return new MatchPrediction
            {
                PredictorName = Name,
                PredictorPriority = Priority,
                FixtureId = context.Fixture.Id,
                HomeTeamId = context.HomeTeam.Id,
                AwayTeamId = context.AwayTeam.Id,
                Outcome = OutcomeProbability,
                Explanation = $"Basado en puntos de ranking FIFA: {context.HomeTeam.Name} {home.Value:0}, {context.AwayTeam.Name} {away.Value:0}.",
                Drivers = new[] { $"Diferencia de puntos FIFA: {Diff:+0;-0}" },
                FeaturesUsed = new[] { "Puntos FIFA del equipo A", "Puntos FIFA del equipo B" },
                FeaturesMissing = Array.Empty<string>(),
                Sources = new[] { SourceMetadata.FifaRankings },
                Degraded = false
            };
        }
    }
}
