using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FindThatBook.Domain.Services;

/// <summary>
/// Normalizes text for comparison: lowercase, diacritics removal, punctuation stripping,
/// noise word removal, and subtitle handling.
/// </summary>
public static class TextNormalizer
{
    private static readonly HashSet<string> NoiseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "of", "and", "by", "in", "on", "at", "to", "for",
        "with", "from", "or", "is", "it", "its", "be", "as"
    };

    /// <summary>
    /// Full normalization pipeline: lowercase → diacritics → punctuation → collapse → noise removal.
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // 1. Lowercase
        var text = input.ToLowerInvariant();

        // 2. Remove diacritics (é → e, ü → u, etc.)
        text = RemoveDiacritics(text);

        // 3. Strip punctuation except hyphens; replace with space
        text = Regex.Replace(text, @"[^\w\s-]", " ");

        // 4. Collapse whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }

    /// <summary>
    /// Normalize and also remove common noise/stop words.
    /// </summary>
    public static string NormalizeForMatching(string? input)
    {
        var normalized = Normalize(input);
        if (string.IsNullOrWhiteSpace(normalized)) return string.Empty;

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                               .Where(t => !NoiseWords.Contains(t));
        return string.Join(" ", tokens);
    }

    /// <summary>
    /// Handles subtitle variants: "The Hobbit, or There and Back Again" → "The Hobbit".
    /// Strips everything after the first " or ", " - ", ": ".
    /// </summary>
    public static string StripSubtitle(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Strip subtitle markers
        var separators = new[] { ", or ", " or ", ": ", " - " };
        var result = input;
        foreach (var sep in separators)
        {
            var idx = result.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                result = result[..idx];
        }
        return result.Trim();
    }

    /// <summary>
    /// Compute Levenshtein distance between two strings for fuzzy matching.
    /// </summary>
    public static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }
        return dp[a.Length, b.Length];
    }

    /// <summary>Returns true if two normalized title strings are a fuzzy match (distance ≤ 3).</summary>
    public static bool IsFuzzyMatch(string a, string b, int maxDistance = 3)
    {
        var normA = NormalizeForMatching(a);
        var normB = NormalizeForMatching(b);
        if (normA == normB) return true;
        return LevenshteinDistance(normA, normB) <= maxDistance;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizedString.Length);
        foreach (var c in normalizedString)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
