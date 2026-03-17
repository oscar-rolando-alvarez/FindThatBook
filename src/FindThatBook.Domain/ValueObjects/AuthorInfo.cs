namespace FindThatBook.Domain.ValueObjects;

/// <summary>
/// Represents an author entry from Open Library, distinguishing primary authors from contributors.
/// Open Library may list illustrators, editors, and adaptors alongside the primary author.
/// </summary>
public record AuthorInfo(
    string Name,
    string? Key,
    string Role = "author"
)
{
    public bool IsPrimaryAuthor => Role.Equals("author", StringComparison.OrdinalIgnoreCase);
}
