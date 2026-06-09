using Oloraculo.Web.Models;
using Oloraculo.Web.Probability;

namespace Oloraculo.Web.Predictors
{
    public class GoalPlusRecentContextModel : GoalModel
    {
        private readonly GoalModel _goalModel;

        public GoalPlusRecentContextModel(IReadOnlyList<MatchResult> results, int yearsWindow = 3)
            : this(new GoalModel(results, yearsWindow))
        {
        }

        public GoalPlusRecentContextModel(GoalModel goalModel)
            : base([], 0)
        {
            _goalModel = goalModel;
        }

        public override string Name => "Goal plus recent context model";
        public override int Priority => 5;

        public override MatchPrediction Predict(MatchContext context)
        {
            var (homeGoals, awayGoals, degradedGoalModel) = _goalModel.ExpectedGoals(context);
            var usedFeatures = new List<string> { nameof(GoalModel) };
            var missingFeatures = new List<string>();
            var drivers = new List<string>();
            var appliedContext = false;

            if (degradedGoalModel)
                missingFeatures.Add("GoalModel required data");

            if (context.FixtureContext is { } ctx)
            {
                if (ctx.UnavailableHomePlayers > 0 || ctx.UnavailableAwayPlayers > 0)
                {
                    homeGoals *= Math.Max(0.86, 1.0 - ctx.UnavailableHomePlayers * 0.02);
                    awayGoals *= Math.Max(0.86, 1.0 - ctx.UnavailableAwayPlayers * 0.02);
                    usedFeatures.Add(nameof(FeaturesEnum.PlayerAvailability));
                    drivers.Add($"{nameof(FeaturesEnum.PlayerAvailability)} applied. Unavailable players: home {ctx.UnavailableHomePlayers}, away {ctx.UnavailableAwayPlayers}.");
                    appliedContext = true;
                }
                else
                {
                    missingFeatures.Add("impactful player availability");
                }

                if (ctx.HasLineups)
                    missingFeatures.Add("lineup impact model");
                else
                    missingFeatures.Add(nameof(FeaturesEnum.Lineups));

                if (ctx.HasOdds)
                    missingFeatures.Add("odds calibration");
                else
                    missingFeatures.Add(nameof(FeaturesEnum.Odds));
            }
            else
            {
                missingFeatures.AddRange([nameof(FeaturesEnum.PlayerAvailability), nameof(FeaturesEnum.Lineups), nameof(FeaturesEnum.Odds)]);
            }

            var scoreline = ProbabilityHelper.PoissonScoreline(homeGoals, awayGoals);
            usedFeatures.AddRange(
            [
                nameof(FeaturesEnum.OpponentAdjustedAttackStrength),
                nameof(FeaturesEnum.OpponentAdjustedDefenseVulnerability),
                nameof(FeaturesEnum.DixonColesScorelineGrid)
            ]);

            var degraded = degradedGoalModel || !appliedContext;
            return new MatchPrediction
            {
                PredictorName = Name,
                PredictorPriority = Priority,
                FixtureId = context.Fixture.Id,
                HomeTeamId = context.HomeTeamId,
                AwayTeamId = context.AwayTeamId,
                Outcome = scoreline.ToOutcome(),
                ExpectedHomeGoals = Math.Round(homeGoals, 2),
                ExpectedAwayGoals = Math.Round(awayGoals, 2),
                Scoreline = scoreline,
                MostLikelyScore = scoreline.MostLikelyScoreline(),
                Explanation = appliedContext
                    ? $"Goal model adjusted by sourced context. Expected goals: {context.HomeTeam.Name} {homeGoals:0.00} - {awayGoals:0.00} {context.AwayTeam.Name}."
                    : $"No sourced match context changed the goal model. Expected goals: {context.HomeTeam.Name} {homeGoals:0.00} - {awayGoals:0.00} {context.AwayTeam.Name}.",
                Drivers = drivers.Count == 0 ? ["No context adjustment applied"] : drivers,
                FeaturesUsed = usedFeatures,
                FeaturesMissing = missingFeatures,
                Sources = [SourceMetadata.HistoricalResultsCsv, SourceMetadata.ApiFootball],
                Degraded = degraded
            };
        }
    }
}
