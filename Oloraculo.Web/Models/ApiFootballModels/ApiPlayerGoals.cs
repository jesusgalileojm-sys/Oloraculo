using System.Text.Json.Serialization;

namespace Oloraculo.Web.Models.ApiFootballModels
{
    public class ApiPlayerGoals
    {
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Total { get; set; }
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int? Assists { get; set; }
    }
}
