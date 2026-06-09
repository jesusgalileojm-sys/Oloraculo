namespace Oloraculo.Web.Models
{
    public class RankingRefreshReport
    {
        public int FifaRows { get; init; }
        public int EloRows { get; init; }
        public DateOnly? FifaRankingDate { get; init; }
        public DateOnly? EloRatingDate { get; init; }
        public IReadOnlyList<string> UpdatedFiles { get; init; } = [];
        public IReadOnlyList<string> Notes { get; init; } = [];
        public IReadOnlyList<string> Errors { get; init; } = [];
        public bool AnyFileUpdated => UpdatedFiles.Count > 0;
    }
}
