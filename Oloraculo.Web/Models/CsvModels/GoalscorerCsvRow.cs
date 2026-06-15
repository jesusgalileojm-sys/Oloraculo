using CsvHelper.Configuration.Attributes;

namespace Oloraculo.Web.Models.CsvModels
{
    public class GoalscorerCsvRow
    {
        [Name("date")]
        public string Date { get; set; } = "";
        [Name("home_team")]
        public string HomeTeam { get; set; } = "";
        [Name("away_team")]
        public string AwayTeam { get; set; } = "";
        [Name("team")]
        public string Team { get; set; } = "";
        [Name("scorer")]
        public string Scorer { get; set; } = "";
        [Name("minute")]
        public string Minute { get; set; } = "";
        [Name("own_goal")]
        public string OwnGoal { get; set; } = "";
        [Name("penalty")]
        public string Penalty { get; set; } = "";
    }
}
