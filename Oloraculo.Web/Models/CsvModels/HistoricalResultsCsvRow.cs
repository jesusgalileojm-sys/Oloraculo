using CsvHelper.Configuration.Attributes;

namespace Oloraculo.Web.Models.CsvModels
{
    public class HistoricalResultCsvRow
    {
        [Name("date")]
        public string Date { get; set; } = "";
        [Name("home_team")]
        public string HomeTeam { get; set; } = "";
        [Name("away_team")]
        public string AwayTeam { get; set; } = "";
        [Name("home_score")]
        public string HomeScore { get; set; } = "";
        [Name("away_score")]
        public string AwayScore { get; set; } = "";
        [Name("tournament")]
        public string Tournament { get; set; } = "";
        [Name("neutral")]
        public string Neutral { get; set; } = "";
    }
}
