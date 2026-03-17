using System.Diagnostics;
using FindThatBook.Application.Common;
using FindThatBook.Application.DTOs;
using FindThatBook.Application.Interfaces;
using FindThatBook.Domain.Entities;
using FindThatBook.Domain.Services;
using Microsoft.Extensions.Logging;

namespace FindThatBook.Application.UseCases;

/// <summary>
/// Orchestrates the full book search pipeline:
/// 1. AI field extraction (with regex fallback)
/// 2. Multi-strategy Open Library search
/// 3. De-duplication and candidate ranking
/// 4. Optional AI re-ranking
/// </summary>
public class SearchBooksUseCase
{
    private readonly IAiExtractor _aiExtractor;
    private readonly IBookRepository _bookRepository;
    private readonly BookMatchingService _matchingService;
    private readonly ILogger<SearchBooksUseCase> _logger;

    public SearchBooksUseCase(
        IAiExtractor aiExtractor,
        IBookRepository bookRepository,
        BookMatchingService matchingService,
        ILogger<SearchBooksUseCase> logger)
    {
        _aiExtractor = aiExtractor;
        _bookRepository = bookRepository;
        _matchingService = matchingService;
        _logger = logger;
    }

    public async Task<Result<BookSearchResponse>> ExecuteAsync(
        BookSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return Result<BookSearchResponse>.Failure("Query cannot be empty.", 400);

        var sw = Stopwatch.StartNew();

        // Step 1: Extract structured fields from the messy query blob
        _logger.LogInformation("Extracting fields from query: {Query}", request.Query);
        var extraction = await _aiExtractor.ExtractAsync(request.Query, cancellationToken);
        _logger.LogInformation("Extraction complete. Method={Method}, Title={Title}, Author={Author}",
            extraction.Method, extraction.Title, extraction.Author);

        var query = new SearchQuery(request.Query);
        query.SetExtraction(extraction);

        // Step 2: Search Open Library
        _logger.LogInformation("Searching Open Library...");
        List<BookCandidate> candidates;
        try
        {
            candidates = await _bookRepository.SearchAsync(extraction, cancellationToken);
            _logger.LogInformation("Found {Count} raw candidates from Open Library.", candidates.Count);
        }
        catch (TaskCanceledException)
        {
            return Result<BookSearchResponse>.Failure("Open Library API timed out. Please try again.", 408);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Open Library API request failed.");
            return Result<BookSearchResponse>.Failure("Could not reach Open Library. Please try again.", 503);
        }

        // Step 3: Score and rank
        var matched = _matchingService.ScoreAndRank(query, candidates, request.MaxResults);

        sw.Stop();

        var response = new BookSearchResponse
        {
            Query = request.Query,
            Extraction = new ExtractionDto
            {
                Title = extraction.Title,
                Author = extraction.Author,
                Keywords = extraction.Keywords,
                Year = extraction.Year,
                Confidence = extraction.Confidence,
                Method = extraction.Method.ToString().ToLowerInvariant(),
                Reasoning = extraction.Reasoning
            },
            Results = matched.Select(m => new BookResultDto
            {
                Title = m.Candidate.Title,
                Author = m.Candidate.PrimaryAuthor?.Name,
                AllAuthors = m.Candidate.Authors.Select(a => $"{a.Name} ({a.Role})").ToList(),
                FirstPublishYear = m.Candidate.FirstPublishYear,
                OpenLibraryWorkId = m.Candidate.WorkId,
                OpenLibraryUrl = m.Candidate.OpenLibraryUrl,
                CoverImageUrl = m.Candidate.CoverImageUrl,
                MatchRank = m.MatchRank,
                MatchType = m.MatchType.ToString(),
                Explanation = m.Explanation
            }).ToList(),
            TotalCandidates = candidates.Count,
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };

        return Result<BookSearchResponse>.Success(response);
    }
}
