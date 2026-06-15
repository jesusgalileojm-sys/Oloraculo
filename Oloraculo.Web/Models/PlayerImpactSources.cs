namespace Oloraculo.Web.Models
{
    public static class PlayerImpactSources
    {
        public const string Position = "position";
        public const string Goalscorers = "goalscorers";
        public const string ApiStats = "api-stats";
        public const string AvailabilityNews = "availability-news";

        public static bool IsFallback(string? source) =>
            string.Equals(source, Position, StringComparison.OrdinalIgnoreCase);

        public static string Combine(params string?[] sources)
        {
            var meaningful = sources
                .Where(source => !string.IsNullOrWhiteSpace(source) && !IsFallback(source))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return meaningful.Count == 0 ? Position : string.Join("+", meaningful);
        }
    }
}
