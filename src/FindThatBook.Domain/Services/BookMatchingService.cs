using FindThatBook.Domain.Entities;
using FindThatBook.Domain.Enums;
using DomainMatchType = FindThatBook.Domain.Enums.MatchType;

namespace FindThatBook.Domain.Services;

/// <summary>
/// Scores and ranks book candidates against the search query using a 5-tier matching hierarchy.
/// Tier 1 (100): Exact title + primary author match (strongest)
/// Tier 2 (80):  Exact title + contributor-only author
/// Tier 3 (60):  Near-match title + author (fuzzy Levenshtein)
/// Tier 4 (40):  Author-only fallback (no title match)
/// Tier 5 (20):  Keyword overlap candidates
/// </summary>
public class BookMatchingService
{
    public List<MatchedBook> ScoreAndRank(SearchQuery query, IEnumerable<BookCandidate> candidates, int maxResults = 5)
    {
        var results = new List<MatchedBook>();

        foreach (var candidate in candidates)
        {
            var matched = Score(query, candidate);
            if (matched != null)
                results.Add(matched);
        }

        // Sort by score descending, then by first publish year (earlier = more canonical)
        var ranked = results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Candidate.FirstPublishYear ?? int.MaxValue)
            .Take(maxResults)
            .ToList();

        for (var i = 0; i < ranked.Count; i++)
            ranked[i].MatchRank = i + 1;

        return ranked;
    }

    private static MatchedBook? Score(SearchQuery query, BookCandidate candidate)
    {
        var extractedTitle = query.Extraction.Title;
        var extractedAuthor = query.Extraction.Author;
        var keywords = query.Extraction.Keywords;

        var normalizedCandidateTitle = TextNormalizer.NormalizeForMatching(candidate.Title);
        var normalizedCandidateTitleNoSub = TextNormalizer.NormalizeForMatching(
            TextNormalizer.StripSubtitle(candidate.Title));

        var normalizedExtractedTitle = TextNormalizer.NormalizeForMatching(extractedTitle);
        var normalizedExtractedAuthor = TextNormalizer.NormalizeForMatching(extractedAuthor);

        bool titleExact = !string.IsNullOrEmpty(normalizedExtractedTitle) &&
                          (normalizedCandidateTitle == normalizedExtractedTitle ||
                           normalizedCandidateTitleNoSub == normalizedExtractedTitle);

        bool titleFuzzy = !string.IsNullOrEmpty(normalizedExtractedTitle) &&
                          !titleExact &&
                          (TextNormalizer.IsFuzzyMatch(normalizedCandidateTitle, normalizedExtractedTitle) ||
                           TextNormalizer.IsFuzzyMatch(normalizedCandidateTitleNoSub, normalizedExtractedTitle));

        // Check author match against all listed authors (primary first)
        bool primaryAuthorMatch = false;
        bool contributorAuthorMatch = false;
        string matchedAuthorName = string.Empty;

        if (!string.IsNullOrEmpty(normalizedExtractedAuthor))
        {
            foreach (var author in candidate.Authors)
            {
                var normalizedCandidateAuthor = TextNormalizer.NormalizeForMatching(author.Name);
                bool authorMatches = normalizedCandidateAuthor.Contains(normalizedExtractedAuthor) ||
                                     normalizedExtractedAuthor.Contains(normalizedCandidateAuthor) ||
                                     TextNormalizer.IsFuzzyMatch(normalizedCandidateAuthor, normalizedExtractedAuthor);
                if (authorMatches)
                {
                    matchedAuthorName = author.Name;
                    if (author.IsPrimaryAuthor)
                        primaryAuthorMatch = true;
                    else
                        contributorAuthorMatch = true;
                }
            }
        }

        // Tier 1: Exact title + primary author
        if (titleExact && primaryAuthorMatch)
            return CreateMatch(candidate, DomainMatchType.ExactTitlePrimaryAuthor, (int)DomainMatchType.ExactTitlePrimaryAuthor,
                $"Exact title match; {matchedAuthorName} is primary author.");

        // Tier 2: Exact title + contributor author
        if (titleExact && contributorAuthorMatch)
            return CreateMatch(candidate, DomainMatchType.ExactTitleContributorAuthor, (int)DomainMatchType.ExactTitleContributorAuthor,
                $"Exact title match; {matchedAuthorName} listed as contributor (not primary author).");

        // Tier 2b: Exact title alone (no author in query)
        if (titleExact && string.IsNullOrEmpty(normalizedExtractedAuthor))
            return CreateMatch(candidate, DomainMatchType.ExactTitleContributorAuthor, (int)DomainMatchType.ExactTitleContributorAuthor,
                $"Exact title match; no author specified in query.");

        // Tier 3: Near-match title + author
        if (titleFuzzy && (primaryAuthorMatch || contributorAuthorMatch))
        {
            var authorNote = primaryAuthorMatch
                ? $"{matchedAuthorName} is primary author."
                : $"{matchedAuthorName} listed as contributor.";
            return CreateMatch(candidate, DomainMatchType.NearMatchTitleAuthor, (int)DomainMatchType.NearMatchTitleAuthor,
                $"Near-match on title; {authorNote}");
        }

        // Tier 3b: Near-match title, no author
        if (titleFuzzy && string.IsNullOrEmpty(normalizedExtractedAuthor))
            return CreateMatch(candidate, DomainMatchType.NearMatchTitleAuthor, (int)DomainMatchType.NearMatchTitleAuthor,
                "Near-match on title; no author specified.");

        // Tier 4: Author-only fallback
        if (!string.IsNullOrEmpty(normalizedExtractedAuthor) && (primaryAuthorMatch || contributorAuthorMatch) && string.IsNullOrEmpty(normalizedExtractedTitle))
            return CreateMatch(candidate, DomainMatchType.AuthorOnlyFallback, (int)DomainMatchType.AuthorOnlyFallback,
                $"Author match ({matchedAuthorName}); no title in query — showing top works.");

        // Tier 5: Keyword candidates
        if (keywords.Length > 0)
        {
            var normalizedCandidateFull = TextNormalizer.Normalize(candidate.Title + " " +
                string.Join(" ", candidate.Authors.Select(a => a.Name)));
            var matchedKeywords = keywords
                .Select(k => TextNormalizer.Normalize(k))
                .Where(k => !string.IsNullOrEmpty(k) && normalizedCandidateFull.Contains(k))
                .ToList();

            if (matchedKeywords.Count >= 2)
                return CreateMatch(candidate, DomainMatchType.KeywordCandidate, (int)DomainMatchType.KeywordCandidate,
                    $"Keyword overlap: [{string.Join(", ", matchedKeywords)}] found in title/author.");
        }

        return null;
    }

    private static MatchedBook CreateMatch(BookCandidate candidate, DomainMatchType matchType, int score, string explanation)
    {
        return new MatchedBook(candidate)
        {
            MatchType = matchType,
            Score = score,
            Explanation = explanation
        };
    }
}
