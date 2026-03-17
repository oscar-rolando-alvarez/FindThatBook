namespace FindThatBook.Domain.Enums;

public enum MatchType
{
    ExactTitlePrimaryAuthor = 100,
    ExactTitleContributorAuthor = 80,
    NearMatchTitleAuthor = 60,
    AuthorOnlyFallback = 40,
    KeywordCandidate = 20
}
