using CsvHelper.Configuration.Attributes;

namespace Oloraculo.Web.Models.CsvModels
{
    public class FifaCsvRow
    {
        [Name("team")]
        public string Team { get; set; } = "";
        [Name("points")]
        public string Points { get; set; } = "";
    }
}
