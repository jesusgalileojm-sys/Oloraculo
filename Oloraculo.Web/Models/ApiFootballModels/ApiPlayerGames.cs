using System.Text.Json.Serialization;

namespace Oloraculo.Web.Models.ApiFootballModels
{
    public class ApiPlayerGames
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Appearences { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Lineups { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Minutes { get; set; }
        public string Position { get; set; } = "";
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Rating { get; set; }
    }
}
