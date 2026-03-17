using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FindThatBook.Application.Interfaces;
using FindThatBook.Domain.Enums;
using FindThatBook.Domain.ValueObjects;
using FindThatBook.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FindThatBook.Infrastructure.AI;

/// <summary>
/// Uses Google Gemini to extract structured fields from messy query blobs.
/// Falls back to RegexFallbackExtractor on any failure (rate limit, timeout, parse error).
/// </summary>
public class GeminiExtractor : IAiExtractor
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly RegexFallbackExtractor _fallback;
    private readonly ILogger<GeminiExtractor> _logger;

    private const string Prompt = """
        You are a librarian assistant. Given a messy text blob that may contain book titles, author names,
        character names, years, and noise tokens, extract structured fields.

        IMPORTANT RULES:
        - "mark huckleberry" → author "Mark Twain", title "Adventures of Huckleberry Finn"
        - "austen bennet" → author "Jane Austen", title "Pride and Prejudice" (Bennet is a character)
        - "twilight meyer" → author "Stephenie Meyer", title "Twilight"
        - Partial last names: "tolkien" → "J.R.R. Tolkien", "dickens" → "Charles Dickens"
        - Numbers like 1937 are publication years, not part of the title
        - Words like "illustrated", "deluxe", "special edition" are edition keywords, not title words

        Return ONLY a valid JSON object with NO markdown, NO code blocks, NO extra text:
        {"title":"string or null","author":"string or null","keywords":["array"],"year":null,"confidence":"high|medium|low","reasoning":"brief explanation"}
        """;

    public GeminiExtractor(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        RegexFallbackExtractor fallback,
        ILogger<GeminiExtractor> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractAsync(string rawQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey == "YOUR_GEMINI_API_KEY")
        {
            _logger.LogWarning("Gemini API key not configured. Using regex fallback.");
            return await _fallback.ExtractAsync(rawQuery, cancellationToken);
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.Model}:generateContent?key={_options.ApiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = Prompt + $"\n\nQuery to analyse: \"{rawQuery}\"" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 512,
                    thinkingConfig = new { thinkingBudget = 0 }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("Gemini returned {StatusCode}. Body: {Body}. Falling back to regex.",
                    (int)response.StatusCode, errorBody);
                return await _fallback.ExtractAsync(rawQuery, cancellationToken);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);
            _logger.LogDebug("Gemini raw response: {Body}", responseBody);

            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseBody);
            var text = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Gemini returned empty text. Full response: {Body}. Falling back.", responseBody);
                return await _fallback.ExtractAsync(rawQuery, cancellationToken);
            }

            _logger.LogInformation("Gemini extracted: {Text}", text);
            return ParseGeminiResponse(text, rawQuery);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Gemini request timed out after {Seconds}s. Using regex fallback.", _options.TimeoutSeconds);
            return await _fallback.ExtractAsync(rawQuery, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling Gemini. Using regex fallback.");
            return await _fallback.ExtractAsync(rawQuery, cancellationToken);
        }
    }

    private ExtractionResult ParseGeminiResponse(string text, string rawQuery)
    {
        try
        {
            // Strip markdown code fences if model ignored instructions
            var cleaned = Regex.Replace(text.Trim(), @"^```(?:json)?\s*|\s*```$", "", RegexOptions.Multiline).Trim();

            // Find first JSON object in the response
            var start = cleaned.IndexOf('{');
            var end = cleaned.LastIndexOf('}');
            if (start >= 0 && end > start)
                cleaned = cleaned[start..(end + 1)];

            var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var title = GetString(root, "title");
            var author = GetString(root, "author");

            var keywords = root.TryGetProperty("keywords", out var kwEl) && kwEl.ValueKind == JsonValueKind.Array
                ? kwEl.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray()
                : Array.Empty<string>();

            int? year = null;
            if (root.TryGetProperty("year", out var yearEl))
            {
                if (yearEl.ValueKind == JsonValueKind.Number) year = yearEl.GetInt32();
                else if (yearEl.ValueKind == JsonValueKind.String && int.TryParse(yearEl.GetString(), out var y)) year = y;
            }

            var confidence = GetString(root, "confidence") ?? "medium";
            var reasoning = GetString(root, "reasoning") ?? string.Empty;

            _logger.LogInformation("Gemini parsed — title={Title} author={Author} confidence={Confidence}", title, author, confidence);
            return new ExtractionResult(title, author, keywords, year, confidence, ExtractionMethod.Gemini, reasoning);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Gemini JSON: {Text}", text);
            return new ExtractionResult(null, null, [], null, "low", ExtractionMethod.RegexFallback,
                $"JSON parse error: {ex.Message}");
        }
    }

    private static string? GetString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private record GeminiResponse(
        [property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates
    );
    private record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent? Content
    );
    private record GeminiContent(
        [property: JsonPropertyName("parts")] List<GeminiPart>? Parts
    );
    private record GeminiPart(
        [property: JsonPropertyName("text")] string? Text
    );
}
