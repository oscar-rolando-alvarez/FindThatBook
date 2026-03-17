using FindThatBook.Domain.Enums;

namespace FindThatBook.Domain.ValueObjects;

/// <summary>
/// The structured output from AI or regex field extraction on a messy query blob.
/// </summary>
public record ExtractionResult(
    string? Title,
    string? Author,
    string[] Keywords,
    int? Year,
    string Confidence,
    ExtractionMethod Method,
    string Reasoning
)
{
    public static ExtractionResult Empty => new(null, null, [], null, "low", ExtractionMethod.RegexFallback, "No fields extracted.");
}
