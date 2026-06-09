namespace Oloraculo.Web.Models
{
    public class ModelPerformanceRow
    {
        public string ModelName { get; init; } = "";
        public int Count { get; init; }
        public double TopPickAccuracy { get; init; }
        public double MeanBrier { get; init; }
        public double MeanRps { get; init; }
        public double MeanLogLoss { get; init; }
    }
}
