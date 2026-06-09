using CsvHelper.Configuration.Attributes;

namespace Oloraculo.Web.Models.CsvModels
{
    public class EloCsvRow
    {
        [Name("team")]
        public string Team { get; set; } = "";
        [Name("elo")]
        public string Elo { get; set; } = "";
    }
}
