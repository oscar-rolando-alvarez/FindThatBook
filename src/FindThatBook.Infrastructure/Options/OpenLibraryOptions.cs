namespace FindThatBook.Infrastructure.Options;

public class OpenLibraryOptions
{
    public const string Section = "OpenLibrary";
    public string BaseUrl { get; set; } = "https://openlibrary.org";
    public int TimeoutSeconds { get; set; } = 5;
    public int MaxSearchResults { get; set; } = 20;
}
