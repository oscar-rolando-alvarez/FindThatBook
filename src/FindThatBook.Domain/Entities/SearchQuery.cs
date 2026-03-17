using FindThatBook.Domain.ValueObjects;

namespace FindThatBook.Domain.Entities;

/// <summary>
/// Encapsulates the original user query along with AI-extracted fields.
/// </summary>
public class SearchQuery
{
    public string RawText { get; }
    public ExtractionResult Extraction { get; private set; }

    public SearchQuery(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            throw new ArgumentException("Query cannot be empty.", nameof(rawText));
        RawText = rawText.Trim();
        Extraction = ExtractionResult.Empty;
    }

    public void SetExtraction(ExtractionResult result)
    {
        Extraction = result ?? throw new ArgumentNullException(nameof(result));
    }

    public bool HasTitle => !string.IsNullOrWhiteSpace(Extraction.Title);
    public bool HasAuthor => !string.IsNullOrWhiteSpace(Extraction.Author);
}
