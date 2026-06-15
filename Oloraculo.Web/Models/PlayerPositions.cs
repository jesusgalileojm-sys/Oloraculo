namespace Oloraculo.Web.Models
{
    public static class PlayerPositions
    {
        public const string Goalkeeper = "Goalkeeper";
        public const string Defender = "Defender";
        public const string Midfielder = "Midfielder";
        public const string Attacker = "Attacker";
        public const string Forward = "Forward";
        public const string Unknown = "Unknown";

        public static bool IsRegularDefensiveRole(string position) =>
            position is Defender or Midfielder or Goalkeeper;
    }
}
