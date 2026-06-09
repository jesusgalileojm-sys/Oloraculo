namespace Oloraculo.Web.Models
{
    public class PredictionEvaluation
    {

        public int Id { get; set; }
        public string ModelName { get; set; }
        public string FixtureId { get; set; }
        public string HomeTeamId { get; set; }
        public string AwayTeamId { get; set; }
        public int HomeGoals { get; set; }
        public int AwayGoals { get; set; }
        public double HomeWin { get; set; }
        public double Draw { get; set; }
        public double AwayWin { get; set; }
        public string Actual { get; set; }
        public double BrierScore { get; set; }
        public double RankedProbabilityScore { get; set; }
        public double LogLoss { get; set; }
        public bool TopPickCorrect { get; set; }
        public DateTimeOffset PredictedAt { get; set; }


    }
}
