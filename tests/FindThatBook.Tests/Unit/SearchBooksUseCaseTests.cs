using FindThatBook.Application.DTOs;
using FindThatBook.Application.Interfaces;
using FindThatBook.Application.UseCases;
using FindThatBook.Domain.Entities;
using FindThatBook.Domain.Enums;
using FindThatBook.Domain.Services;
using FindThatBook.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FindThatBook.Tests.Unit;

public class SearchBooksUseCaseTests
{
    private readonly Mock<IAiExtractor> _extractorMock = new();
    private readonly Mock<IBookRepository> _repoMock = new();
    private readonly BookMatchingService _matchingService = new();
    private readonly SearchBooksUseCase _sut;

    public SearchBooksUseCaseTests()
    {
        _sut = new SearchBooksUseCase(
            _extractorMock.Object,
            _repoMock.Object,
            _matchingService,
            NullLogger<SearchBooksUseCase>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyQuery_ReturnsFailure()
    {
        var result = await _sut.ExecuteAsync(new BookSearchRequest { Query = "" });
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(400);
    }

    [Fact]
    public async Task ExecuteAsync_ValidQuery_ReturnsSuccess()
    {
        _extractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractionResult("The Hobbit", "J.R.R. Tolkien", [], 1937, "high", ExtractionMethod.Gemini, "test"));

        _repoMock
            .Setup(r => r.SearchAsync(It.IsAny<ExtractionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new BookCandidate
                {
                    Title = "The Hobbit",
                    Authors = [new AuthorInfo("J.R.R. Tolkien", "/authors/OL26320A", "author")],
                    FirstPublishYear = 1937,
                    WorkId = "/works/OL45804W"
                }
            ]);

        var result = await _sut.ExecuteAsync(new BookSearchRequest { Query = "tolkien hobbit 1937" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Results.Should().HaveCount(1);
        result.Value.Results[0].Title.Should().Be("The Hobbit");
        result.Value.Results[0].MatchRank.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_OpenLibraryTimeout_ReturnsTimeoutFailure()
    {
        _extractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractionResult("The Hobbit", null, [], null, "low", ExtractionMethod.RegexFallback, ""));

        _repoMock
            .Setup(r => r.SearchAsync(It.IsAny<ExtractionResult>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());

        var result = await _sut.ExecuteAsync(new BookSearchRequest { Query = "hobbit" });

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(408);
    }

    [Fact]
    public async Task ExecuteAsync_NoMatches_ReturnsSuccessWithEmptyResults()
    {
        _extractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractionResult("XYZ Book", null, [], null, "low", ExtractionMethod.RegexFallback, ""));

        _repoMock
            .Setup(r => r.SearchAsync(It.IsAny<ExtractionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.ExecuteAsync(new BookSearchRequest { Query = "xyz book" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_IncludesExtractionInResponse()
    {
        var extraction = new ExtractionResult("The Hobbit", "J.R.R. Tolkien", ["illustrated"], 1937, "high", ExtractionMethod.Gemini, "test reasoning");

        _extractorMock
            .Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extraction);

        _repoMock
            .Setup(r => r.SearchAsync(It.IsAny<ExtractionResult>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.ExecuteAsync(new BookSearchRequest { Query = "tolkien hobbit illustrated 1937" });

        result.Value!.Extraction.Title.Should().Be("The Hobbit");
        result.Value.Extraction.Author.Should().Be("J.R.R. Tolkien");
        result.Value.Extraction.Year.Should().Be(1937);
        result.Value.Extraction.Method.Should().Be("gemini");
    }
}
