using FindThatBook.Domain.Services;
using FluentAssertions;

namespace FindThatBook.Tests.Unit;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Tolkien", "tolkien")]
    [InlineData("THE HOBBIT", "the hobbit")]
    [InlineData("café", "cafe")]
    [InlineData("  extra   spaces  ", "extra spaces")]
    [InlineData("title, with punctuation!", "title with punctuation")]
    public void Normalize_ShouldProduceExpectedOutput(string input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_EmptyOrNull_ReturnsEmpty(string? input, string expected)
    {
        TextNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void NormalizeForMatching_RemovesNoiseWords()
    {
        var result = TextNormalizer.NormalizeForMatching("The Adventures of Huckleberry Finn");
        result.Should().NotContain("the");
        result.Should().NotContain("of");
        result.Should().Contain("adventures");
        result.Should().Contain("huckleberry");
    }

    [Theory]
    [InlineData("The Hobbit, or There and Back Again", "The Hobbit")]
    [InlineData("Pride and Prejudice: A Novel", "Pride and Prejudice")]
    [InlineData("A Tale of Two Cities - Classic Edition", "A Tale of Two Cities")]
    [InlineData("The Hobbit", "The Hobbit")]
    public void StripSubtitle_RemovesSubtitleVariants(string input, string expected)
    {
        TextNormalizer.StripSubtitle(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("hobbit", "hobbit", 0)]
    [InlineData("hobbit", "hobbyt", 1)]
    [InlineData("hobbit", "hobbit2", 1)]
    [InlineData("kitten", "sitting", 3)]
    public void LevenshteinDistance_ReturnsCorrectDistance(string a, string b, int expected)
    {
        TextNormalizer.LevenshteinDistance(a, b).Should().Be(expected);
    }

    [Theory]
    [InlineData("hobbit", "hobbyt", true)]    // distance 1
    [InlineData("twilight", "twilight", true)] // exact
    [InlineData("dickens", "dickons", true)]   // distance 1
    [InlineData("hobbit", "hamlet", false)]    // distance 5
    public void IsFuzzyMatch_CorrectlyIdentifiesMatches(string a, string b, bool expected)
    {
        TextNormalizer.IsFuzzyMatch(a, b).Should().Be(expected);
    }
}
