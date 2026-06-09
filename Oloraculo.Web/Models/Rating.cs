namespace Oloraculo.Web.Models
{
    public class Rating
    {
        public int Id { get; set; }
        public string TeamId { get; set; }
        public RatingTypeEnum Type { get; set; }
        public double Value { get; set; }
        public DateTimeOffset AsOf { get; set; }
        public string Source { get; set; }

    }
}
