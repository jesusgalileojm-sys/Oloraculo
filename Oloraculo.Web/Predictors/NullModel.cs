using Oloraculo.Web.Models;
using Oloraculo.Web.Probability;

namespace Oloraculo.Web.Predictors
{
    public class NullModel : IPredictor
    {
        public string Name => "NullModel";
        public int Priority => 0;
        public MatchPrediction Predict(MatchContext context) 
        {
            return new MatchPrediction
            {
                PredictorName = Name,
                PredictorPriority = Priority,
                FixtureId = context.Fixture.Id,
                HomeTeamId = context.HomeTeam.Id,
                AwayTeamId = context.AwayTeam.Id,
                Outcome = OutcomeProbabilities.Uniform,
                ExpectedHomeGoals = null,
                ExpectedAwayGoals = null,
                Scoreline = null,
                MostLikelyScore = null,
                Explanation = "Ni idea, jaja salu2",
                Drivers = Array.Empty<string>(),
                FeaturesUsed = Array.Empty<string>(),
                FeaturesMissing = Array.Empty<string>(),
                Sources = Array.Empty<SourceMetadata>(),
                Degraded = false
            };
        }
    }
}
