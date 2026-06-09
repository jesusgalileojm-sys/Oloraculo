using CsvHelper.Configuration.Attributes;

namespace Oloraculo.Web.Models.CsvModels
{
    public class FifaCsvRow
    {
        [Name("rank")]
        public int Rank { get; set; }
        [Name("team")]
        public string Team { get; set; } = "";
        [Name("rank_change_since_previous")]
        public int RankChangeSincePrevious { get; set; }
        [Name("points")]
        public string Points { get; set; } = "";
        [Name("ranking_date")]
        public string RankingDate { get; set; } = "";
    }
}
