using Oloraculo.Web.Models;

namespace Oloraculo.Web.Predictors
{
    public interface IPredictor
    {
        string Name { get; }
        int Priority { get; }
        MatchPrediction Predict(MatchContext context);
    }
}
