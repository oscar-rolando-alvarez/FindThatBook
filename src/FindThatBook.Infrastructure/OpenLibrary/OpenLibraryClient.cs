using System.Net.Http.Json;
using System.Text.Json;
using FindThatBook.Application.DTOs;
using FindThatBook.Application.Interfaces;
using FindThatBook.Domain.Entities;
using FindThatBook.Domain.Services;
using FindThatBook.Domain.ValueObjects;
using FindThatBook.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FindThatBook.Infrastructure.OpenLibrary;

/// <summary>
/// Implements IBookRepository using the Open Library public API.
/// Uses multi-strategy search: precise (title+author), broad (full query), author-only fallback.
/// De-duplicates by work_id and resolves primary authors from work records.
/// </summary>
public class OpenLibraryClient : IBookRepository
{
    private readonly HttpClient _httpClient;
    private readonly OpenLibraryOptions _options;
    private readonly ILogger<OpenLibraryClient> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public OpenLibraryClient(
        HttpClient httpClient,
        IOptions<OpenLibraryOptions> options,
        ILogger<OpenLibraryClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<BookCandidate>> SearchAsync(ExtractionResult extraction, CancellationToken cancellationToken = default)
    {
        var allDocs = new Dictionary<string, SearchDoc>(StringComparer.OrdinalIgnoreCase);

        // Strategy 1: Title + Author search (most precise)
        if (!string.IsNullOrWhiteSpace(extraction.Title) && !string.IsNullOrWhiteSpace(extraction.Author))
        {
            var docs = await SearchByTitleAndAuthorAsync(extraction.Title, extraction.Author, cancellationToken);
            MergeDocs(allDocs, docs);
        }

        // Strategy 2: Title-only search
        if (!string.IsNullOrWhiteSpace(extraction.Title))
        {
            var docs = await SearchByQueryAsync(extraction.Title, cancellationToken);
            MergeDocs(allDocs, docs);
        }

        // Strategy 3: Author-only fallback (if no title or few results so far)
        if (!string.IsNullOrWhiteSpace(extraction.Author) && allDocs.Count < 5)
        {
            var docs = await SearchByQueryAsync(extraction.Author, cancellationToken);
            MergeDocs(allDocs, docs);
        }

        // Strategy 4: Full raw-keyword search for keyword-only queries
        if (extraction.Keywords.Length > 0 && allDocs.Count < 3)
        {
            var kwQuery = string.Join(" ", extraction.Keywords.Take(3));
            var docs = await SearchByQueryAsync(kwQuery, cancellationToken);
            MergeDocs(allDocs, docs);
        }

        // Strategy 5: Broad search on full reasoning/title if still empty (handles regex fallback edge cases)
        if (allDocs.Count == 0 && !string.IsNullOrWhiteSpace(extraction.Title))
        {
            // Try title without normalization as a last resort
            var docs = await SearchByQueryAsync(extraction.Title, cancellationToken);
            MergeDocs(allDocs, docs);
        }

        _logger.LogInformation("De-duplicated to {Count} unique work candidates.", allDocs.Count);

        return allDocs.Values
            .Take(_options.MaxSearchResults)
            .Select(MapToCandidate)
            .ToList();
    }

    public async Task<BookDetailResponse?> GetWorkDetailsAsync(string workId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Normalize workId: ensure it starts with /works/
            var path = workId.StartsWith("/works/") ? workId : $"/works/{workId}";
            var work = await _httpClient.GetFromJsonAsync<WorkResponse>($"{path}.json", cancellationToken);
            if (work == null) return null;

            var authors = await ResolveAuthorsAsync(work.Authors, cancellationToken);
            var description = ExtractDescription(work.Description);
            var coverId = work.Covers?.FirstOrDefault();

            int? firstYear = null;
            if (!string.IsNullOrEmpty(work.FirstPublishDate))
            {
                var match = System.Text.RegularExpressions.Regex.Match(work.FirstPublishDate, @"\d{4}");
                if (match.Success) firstYear = int.Parse(match.Value);
            }

            return new BookDetailResponse
            {
                WorkId = path,
                Title = work.Title ?? "Unknown",
                Author = authors.FirstOrDefault(a => a.IsPrimaryAuthor)?.Name ?? authors.FirstOrDefault()?.Name,
                AllAuthors = authors.Select(a => $"{a.Name} ({a.Role})").ToList(),
                FirstPublishYear = firstYear,
                Description = description,
                CoverImageUrl = coverId.HasValue ? $"https://covers.openlibrary.org/b/id/{coverId}-M.jpg" : null,
                OpenLibraryUrl = $"https://openlibrary.org{path}",
                Subjects = work.Subjects?.Take(10).ToList() ?? []
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch work details for {WorkId}", workId);
            return null;
        }
    }

    private async Task<List<SearchDoc>> SearchByTitleAndAuthorAsync(string title, string author, CancellationToken ct)
    {
        var normalizedTitle = Uri.EscapeDataString(TextNormalizer.Normalize(title));
        var normalizedAuthor = Uri.EscapeDataString(TextNormalizer.Normalize(author));
        var url = $"/search.json?title={normalizedTitle}&author={normalizedAuthor}&limit={_options.MaxSearchResults}&fields=key,title,author_name,author_key,first_publish_year,cover_i,edition_count";
        return await ExecuteSearchAsync(url, ct);
    }

    private async Task<List<SearchDoc>> SearchByQueryAsync(string query, CancellationToken ct)
    {
        var normalizedQuery = Uri.EscapeDataString(TextNormalizer.Normalize(query));
        var url = $"/search.json?q={normalizedQuery}&limit={_options.MaxSearchResults}&fields=key,title,author_name,author_key,first_publish_year,cover_i,edition_count";
        return await ExecuteSearchAsync(url, ct);
    }

    private async Task<List<SearchDoc>> ExecuteSearchAsync(string url, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Open Library request: GET {Url}", url);
            var response = await _httpClient.GetFromJsonAsync<SearchResponse>(url, ct);
            return response?.Docs ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Search request failed for URL: {Url}", url);
            return [];
        }
    }

    private static void MergeDocs(Dictionary<string, SearchDoc> target, List<SearchDoc> source)
    {
        foreach (var doc in source)
        {
            if (!string.IsNullOrWhiteSpace(doc.Key) && !target.ContainsKey(doc.Key))
                target[doc.Key] = doc;
        }
    }

    private static BookCandidate MapToCandidate(SearchDoc doc)
    {
        var authors = new List<AuthorInfo>();
        if (doc.AuthorName != null)
        {
            for (var i = 0; i < doc.AuthorName.Count; i++)
            {
                var key = doc.AuthorKey != null && i < doc.AuthorKey.Count ? doc.AuthorKey[i] : null;
                // First listed author is treated as primary
                authors.Add(new AuthorInfo(doc.AuthorName[i], key, i == 0 ? "author" : "contributor"));
            }
        }

        return new BookCandidate
        {
            Title = doc.Title ?? "Unknown",
            Authors = authors,
            FirstPublishYear = doc.FirstPublishYear,
            WorkId = doc.Key ?? string.Empty,
            CoverIds = doc.CoverId.HasValue ? [doc.CoverId.Value] : []
        };
    }

    private async Task<List<AuthorInfo>> ResolveAuthorsAsync(
        List<WorkAuthorEntry>? workAuthors, CancellationToken ct)
    {
        if (workAuthors == null || workAuthors.Count == 0) return [];

        var resolved = new List<AuthorInfo>();
        foreach (var entry in workAuthors.Take(5))
        {
            if (string.IsNullOrWhiteSpace(entry.Author?.Key)) continue;
            try
            {
                var author = await _httpClient.GetFromJsonAsync<AuthorResponse>(
                    $"{entry.Author.Key}.json", ct);
                if (author != null)
                {
                    var role = string.IsNullOrWhiteSpace(entry.Role) ? "author" : entry.Role.ToLowerInvariant();
                    resolved.Add(new AuthorInfo(author.Name ?? author.PersonalName ?? "Unknown", author.Key, role));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve author {Key}", entry.Author.Key);
            }
        }
        return resolved;
    }

    private static string? ExtractDescription(object? description)
    {
        if (description == null) return null;
        if (description is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String) return el.GetString();
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("value", out var val))
                return val.GetString();
        }
        return description.ToString();
    }
}
