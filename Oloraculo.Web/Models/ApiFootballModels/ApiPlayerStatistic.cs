namespace Oloraculo.Web.Models.ApiFootballModels
{
    public class ApiPlayerStatistic
    {
        public ApiPlayerGames Games { get; set; } = new();
        public ApiPlayerGoals Goals { get; set; } = new();
    }
}
