using FindThatBook.Domain.Enums;
using DomainMatchType = FindThatBook.Domain.Enums.MatchType;

namespace FindThatBook.Domain.Entities;

/// <summary>
/// A ranked, scored book result with explanation — the final output per candidate.
/// </summary>
public class MatchedBook
{
    public BookCandidate Candidate { get; }
    public int MatchRank { get; set; }
    public DomainMatchType MatchType { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public int Score { get; set; }

    public MatchedBook(BookCandidate candidate)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }
}
