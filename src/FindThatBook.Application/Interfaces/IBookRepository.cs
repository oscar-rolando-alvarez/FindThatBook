using FindThatBook.Application.DTOs;
using FindThatBook.Domain.Entities;
using FindThatBook.Domain.ValueObjects;

namespace FindThatBook.Application.Interfaces;

/// <summary>
/// Repository abstraction for searching and fetching book data from Open Library.
/// </summary>
public interface IBookRepository
{
    /// <summary>Search for book candidates using AI-extracted fields as query parameters.</summary>
    Task<List<BookCandidate>> SearchAsync(ExtractionResult extraction, CancellationToken cancellationToken = default);

    /// <summary>Get full details for a specific Open Library work by its work ID (e.g. /works/OL45804W).</summary>
    Task<BookDetailResponse?> GetWorkDetailsAsync(string workId, CancellationToken cancellationToken = default);
}
