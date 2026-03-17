using FindThatBook.Domain.Enums;
using FindThatBook.Domain.ValueObjects;
using FindThatBook.Infrastructure.AI;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindThatBook.Tests.Unit;

public class RegexFallbackExtractorTests
{
    private readonly RegexFallbackExtractor _sut = new(NullLogger<RegexFallbackExtractor>.Instance);

    [Fact]
    public async Task ExtractAsync_EmptyQuery_ReturnsEmpty()
    {
        var result = await _sut.ExtractAsync("");
        result.Should().Be(ExtractionResult.Empty);
    }

    [Theory]
    [InlineData("tolkien hobbit 1937", 1937)]
    [InlineData("dickens tale 1859", 1859)]
    [InlineData("twilight", null)]
    public async Task ExtractAsync_DetectsYear(string query, int? expectedYear)
    {
        var result = await _sut.ExtractAsync(query);
        result.Year.Should().Be(expectedYear);
    }

    [Fact]
    public async Task ExtractAsync_UsesRegexFallbackMethod()
    {
        var result = await _sut.ExtractAsync("some book query");
        result.Method.Should().Be(ExtractionMethod.RegexFallback);
    }

    [Fact]
    public async Task ExtractAsync_LowConfidence()
    {
        var result = await _sut.ExtractAsync("some query");
        result.Confidence.Should().Be("low");
    }

    [Fact]
    public async Task ExtractAsync_NullQuery_ReturnsEmpty()
    {
        var result = await _sut.ExtractAsync(null!);
        result.Should().Be(ExtractionResult.Empty);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsMeaningfulTokensAsTitle()
    {
        var result = await _sut.ExtractAsync("hobbit tolkien 1937");
        result.Title.Should().NotBeNullOrEmpty();
        result.Title!.ToLower().Should().ContainAny("hobbit", "tolkien");
    }
}
