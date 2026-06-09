using CsvHelper.Configuration.Attributes;

namespace Oloraculo.Web.Models.CsvModels
{
    public class EloCsvRow
    {
        [Name("rank")]
        public int Rank { get; set; }
        [Name("team")]
        public string Team { get; set; } = "";
        [Name("elo_rating")]
        public string Elo { get; set; } = "";
        [Name("rating_date")]
        public string RatingDate { get; set; } = "";
        [Name("source")]
        public string Source { get; set; } = "";
        [Name("source_original")]
        public string SourceOriginal { get; set; } = "";
    }
}
