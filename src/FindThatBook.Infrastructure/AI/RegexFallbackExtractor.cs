using System.Text.RegularExpressions;
using FindThatBook.Application.Interfaces;
using FindThatBook.Domain.Enums;
using FindThatBook.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FindThatBook.Infrastructure.AI;

/// <summary>
/// Fallback extractor using heuristics when Gemini is unavailable.
/// Detects years (4-digit numbers), strips noise words, infers title/author from word patterns.
/// </summary>
public class RegexFallbackExtractor : IAiExtractor
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "of", "and", "by", "in", "on", "at", "to", "for",
        "with", "from", "illustrated", "deluxe", "special", "edition", "volume",
        "series", "book", "novel", "complete", "revised", "updated", "new"
    };

    private readonly ILogger<RegexFallbackExtractor> _logger;

    public RegexFallbackExtractor(ILogger<RegexFallbackExtractor> logger)
    {
        _logger = logger;
    }

    public Task<ExtractionResult> ExtractAsync(string rawQuery, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Using regex fallback extractor for query: {Query}", rawQuery);

        if (string.IsNullOrWhiteSpace(rawQuery))
            return Task.FromResult(ExtractionResult.Empty);

        var text = rawQuery.Trim();

        // Extract 4-digit year
        var yearMatch = Regex.Match(text, @"\b(1[6-9]\d{2}|20[0-2]\d)\b");
        int? year = yearMatch.Success ? int.Parse(yearMatch.Value) : null;

        // Remove year and punctuation for token analysis
        var cleaned = Regex.Replace(text, @"\b(1[6-9]\d{2}|20[0-2]\d)\b", "");
        cleaned = Regex.Replace(cleaned, @"[^\w\s]", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();

        var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                            .Where(t => t.Length > 2)
                            .ToList();

        var meaningfulTokens = tokens.Where(t => !StopWords.Contains(t)).ToList();
        var noiseKeywords = tokens.Where(t => StopWords.Contains(t)).ToList();

        // Heuristic: treat meaningful tokens as potential title words
        string? title = meaningfulTokens.Count > 0
            ? string.Join(" ", meaningfulTokens.Take(4))
            : null;

        var reasoning = $"Regex fallback: extracted {meaningfulTokens.Count} meaningful tokens. Year={year?.ToString() ?? "none"}.";

        return Task.FromResult(new ExtractionResult(
            Title: title,
            Author: null, // Regex can't reliably distinguish author from title
            Keywords: BuildKeywords(noiseKeywords, meaningfulTokens),
            Year: year,
            Confidence: "low",
            Method: ExtractionMethod.RegexFallback,
            Reasoning: reasoning
        ));
    }

    private static string[] BuildKeywords(List<string> noiseKeywords, List<string> meaningfulTokens)
    {
        var extra = meaningfulTokens.Count > 4 ? meaningfulTokens.GetRange(4, meaningfulTokens.Count - 4) : new List<string>();
        var result = new List<string>(noiseKeywords);
        result.AddRange(extra);
        return result.ToArray();
    }
}
