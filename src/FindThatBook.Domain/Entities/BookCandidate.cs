using FindThatBook.Domain.ValueObjects;

namespace FindThatBook.Domain.Entities;

/// <summary>
/// A raw book result from Open Library before scoring and ranking.
/// </summary>
public class BookCandidate
{
    public string Title { get; set; } = string.Empty;
    public List<AuthorInfo> Authors { get; set; } = [];
    public int? FirstPublishYear { get; set; }
    public string WorkId { get; set; } = string.Empty;
    public List<long> CoverIds { get; set; } = [];

    public string OpenLibraryUrl => string.IsNullOrWhiteSpace(WorkId)
        ? string.Empty
        : $"https://openlibrary.org{WorkId}";

    public string? CoverImageUrl => CoverIds.Count > 0
        ? $"https://covers.openlibrary.org/b/id/{CoverIds[0]}-M.jpg"
        : null;

    /// <summary>The primary (non-contributor) author, if one exists.</summary>
    public AuthorInfo? PrimaryAuthor => Authors.FirstOrDefault(a => a.IsPrimaryAuthor)
                                         ?? Authors.FirstOrDefault();
}
