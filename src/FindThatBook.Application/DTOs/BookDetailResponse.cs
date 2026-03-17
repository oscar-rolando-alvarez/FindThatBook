namespace FindThatBook.Application.DTOs;

public class BookDetailResponse
{
    public string WorkId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Author { get; set; }
    public List<string> AllAuthors { get; set; } = [];
    public int? FirstPublishYear { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public string OpenLibraryUrl { get; set; } = string.Empty;
    public List<string> Subjects { get; set; } = [];
}
