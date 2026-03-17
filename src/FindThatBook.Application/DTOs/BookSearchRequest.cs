using System.ComponentModel.DataAnnotations;

namespace FindThatBook.Application.DTOs;

public class BookSearchRequest
{
    /// <summary>
    /// Messy plain-text query: may include title fragments, author names, character names, years, noise tokens.
    /// Examples: "tolkien hobbit illustrated deluxe 1937", "mark huckleberry", "austen bennet"
    /// </summary>
    [Required]
    [MinLength(1)]
    public string Query { get; set; } = string.Empty;

    /// <summary>Maximum number of results to return (1–10).</summary>
    [Range(1, 10)]
    public int MaxResults { get; set; } = 5;

    /// <summary>If true, re-rank top candidates using a second AI pass for better ordering.</summary>
    public bool EnableAiReranking { get; set; } = false;
}
