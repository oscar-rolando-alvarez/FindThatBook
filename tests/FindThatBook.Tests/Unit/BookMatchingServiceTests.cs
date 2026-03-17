using FindThatBook.Domain.Entities;
using FindThatBook.Domain.Enums;
using FindThatBook.Domain.Services;
using FindThatBook.Domain.ValueObjects;
using FluentAssertions;
using DomainMatchType = FindThatBook.Domain.Enums.MatchType;

namespace FindThatBook.Tests.Unit;

public class BookMatchingServiceTests
{
    private readonly BookMatchingService _sut = new();

    private static SearchQuery BuildQuery(string raw, string? title, string? author, string[] keywords = null!)
    {
        var q = new SearchQuery(raw);
        q.SetExtraction(new ExtractionResult(title, author, keywords ?? [], null, "high", ExtractionMethod.Gemini, "test"));
        return q;
    }

    private static BookCandidate MakeCandidate(string title, string authorName, string role = "author", int? year = null) =>
        new()
        {
            Title = title,
            Authors = [new AuthorInfo(authorName, "/authors/test", role)],
            FirstPublishYear = year,
            WorkId = "/works/OL123W"
        };

    [Fact]
    public void ScoreAndRank_ExactTitlePrimaryAuthor_GetsTier1()
    {
        var query = BuildQuery("tolkien hobbit", "The Hobbit", "J.R.R. Tolkien");
        var candidate = MakeCandidate("The Hobbit", "J.R.R. Tolkien", "author");

        var results = _sut.ScoreAndRank(query, [candidate]);

        results.Should().HaveCount(1);
        results[0].MatchType.Should().Be(DomainMatchType.ExactTitlePrimaryAuthor);
        results[0].Score.Should().Be(100);
    }

    [Fact]
    public void ScoreAndRank_ExactTitleContributorAuthor_GetsTier2()
    {
        var query = BuildQuery("hobbit tolkien", "The Hobbit", "J.R.R. Tolkien");
        var candidate = MakeCandidate("The Hobbit", "J.R.R. Tolkien", "editor");

        var results = _sut.ScoreAndRank(query, [candidate]);

        results.Should().HaveCount(1);
        results[0].MatchType.Should().Be(DomainMatchType.ExactTitleContributorAuthor);
        results[0].Score.Should().Be(80);
    }

    [Fact]
    public void ScoreAndRank_AuthorOnly_GetsTier4()
    {
        var query = BuildQuery("tolkien", null, "J.R.R. Tolkien");
        var candidate = MakeCandidate("The Hobbit", "J.R.R. Tolkien");

        var results = _sut.ScoreAndRank(query, [candidate]);

        results.Should().HaveCount(1);
        results[0].MatchType.Should().Be(DomainMatchType.AuthorOnlyFallback);
    }

    [Fact]
    public void ScoreAndRank_FuzzyTitleMatch_GetsTier3()
    {
        var query = BuildQuery("hobit tolkien", "hobit", "J.R.R. Tolkien"); // typo
        var candidate = MakeCandidate("The Hobbit", "J.R.R. Tolkien");

        var results = _sut.ScoreAndRank(query, [candidate]);

        results.Should().HaveCount(1);
        results[0].MatchType.Should().Be(DomainMatchType.NearMatchTitleAuthor);
        results[0].Score.Should().Be(60);
    }

    [Fact]
    public void ScoreAndRank_ReturnsMax5Results()
    {
        var query = BuildQuery("hobbit", "The Hobbit", null);
        var candidates = Enumerable.Range(1, 10)
            .Select(i => MakeCandidate("The Hobbit", $"Author {i}"))
            .ToList();

        var results = _sut.ScoreAndRank(query, candidates, 5);

        results.Should().HaveCount(5);
    }

    [Fact]
    public void ScoreAndRank_SortedByScoreDescending()
    {
        var query = BuildQuery("hobbit tolkien", "The Hobbit", "J.R.R. Tolkien");
        var tier1 = MakeCandidate("The Hobbit", "J.R.R. Tolkien", "author");
        var tier2 = MakeCandidate("The Hobbit", "J.R.R. Tolkien", "editor");

        var results = _sut.ScoreAndRank(query, [tier2, tier1]);

        results[0].Score.Should().BeGreaterThan(results[1].Score);
        results[0].MatchType.Should().Be(DomainMatchType.ExactTitlePrimaryAuthor);
    }

    [Fact]
    public void ScoreAndRank_SubtitleVariant_StillMatches()
    {
        // "The Hobbit, or There and Back Again" should match query title "The Hobbit"
        var query = BuildQuery("tolkien hobbit", "The Hobbit", "J.R.R. Tolkien");
        var candidate = MakeCandidate("The Hobbit, or There and Back Again", "J.R.R. Tolkien");

        var results = _sut.ScoreAndRank(query, [candidate]);

        results.Should().HaveCount(1);
        results[0].MatchType.Should().Be(DomainMatchType.ExactTitlePrimaryAuthor);
    }

    [Fact]
    public void ScoreAndRank_AssignsCorrectRanks()
    {
        var query = BuildQuery("hobbit tolkien", "The Hobbit", "J.R.R. Tolkien");
        var c1 = MakeCandidate("The Hobbit", "J.R.R. Tolkien", "author", 1937);
        var c2 = MakeCandidate("The Hobbit", "J.R.R. Tolkien", "editor", 1966);

        var results = _sut.ScoreAndRank(query, [c2, c1]);

        results[0].MatchRank.Should().Be(1);
        results[1].MatchRank.Should().Be(2);
    }
}
