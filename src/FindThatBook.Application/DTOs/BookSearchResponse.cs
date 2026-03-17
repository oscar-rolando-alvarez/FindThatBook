namespace FindThatBook.Application.DTOs;

public class BookSearchResponse
{
    public string Query { get; set; } = string.Empty;
    public ExtractionDto Extraction { get; set; } = new();
    public List<BookResultDto> Results { get; set; } = [];
    public int TotalCandidates { get; set; }
    public long ProcessingTimeMs { get; set; }
}

public class ExtractionDto
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string[] Keywords { get; set; } = [];
    public int? Year { get; set; }
    public string Confidence { get; set; } = "low";
    public string Method { get; set; } = "regex";
    public string Reasoning { get; set; } = string.Empty;
}

public class BookResultDto
{
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public List<string> AllAuthors { get; set; } = [];
    public int? FirstPublishYear { get; set; }
    public string OpenLibraryWorkId { get; set; } = string.Empty;
    public string OpenLibraryUrl { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public int MatchRank { get; set; }
    public string MatchType { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}
