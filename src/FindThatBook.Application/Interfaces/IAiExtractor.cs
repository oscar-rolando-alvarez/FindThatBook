using FindThatBook.Domain.ValueObjects;

namespace FindThatBook.Application.Interfaces;

/// <summary>
/// Strategy interface for extracting structured fields from a messy query blob.
/// Implementations: GeminiExtractor (primary), RegexFallbackExtractor (fallback).
/// </summary>
public interface IAiExtractor
{
    Task<ExtractionResult> ExtractAsync(string rawQuery, CancellationToken cancellationToken = default);
}
