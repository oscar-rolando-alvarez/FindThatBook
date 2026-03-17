# Changes & Build History

A full record of every design decision, fix, and evolution from blank slate to working application.

---

## Phase 1 — Initial Build

**Scope:** Full solution built from the InfoTrack requirements documents.

### What was created

**Solution structure**
- `FindThatBook.slnx` — .NET solution linking all projects
- 4 C# projects: `Domain`, `Application`, `Infrastructure`, `Api`
- 1 test project: `FindThatBook.Tests` (xUnit + Moq + FluentAssertions)
- React 19 frontend in `client/`

**Domain layer**
- `SearchQuery`, `BookCandidate`, `MatchedBook` entities
- `ExtractionResult`, `AuthorInfo` value objects
- `MatchType` (5-tier enum), `ExtractionMethod` enum
- `TextNormalizer`: normalize, strip subtitle, Levenshtein distance, fuzzy match
- `BookMatchingService`: 5-tier scoring with tie-breaking by year

**Application layer**
- `Result<T>` railway-oriented error handling
- `IAiExtractor`, `IBookRepository` port interfaces
- `SearchBooksUseCase` orchestration pipeline
- DTOs: `BookSearchRequest`, `BookSearchResponse`, `BookDetailResponse`, etc.

**Infrastructure layer**
- `GeminiExtractor`: Gemini API integration with regex fallback
- `RegexFallbackExtractor`: heuristic year/token extraction
- `OpenLibraryClient`: multi-strategy search (5 strategies) + work details
- `ServiceCollectionExtensions`: typed HttpClient DI registration

**API layer**
- `BookSearchController`: `POST /search` + `GET /{workId}`
- `HealthController`: `GET /health`
- `GlobalExceptionMiddleware`: RFC 7807 ProblemDetails with correlation IDs
- Swagger/OpenAPI configured with XML comments

**Frontend**
- Vite 5 + React 19 + TypeScript
- `SearchBar`, `ExtractionPanel`, `ResultCard`, `LoadingSkeleton` components
- TanStack React Query for async state management
- Inline styles (CSS-in-JS objects) to avoid Tailwind v4 / Node 20 incompatibility
- Axios HTTP client with typed API layer

**Tests**
- 43 unit tests across 4 files — all passing

---

## Phase 2 — CORS Fix

**Problem:** The React frontend (`:5173`) made requests to the API and got CORS errors.

**Root causes (two separate issues):**

1. **Wrong proxy port in `vite.config.ts`**
   - Initial config forwarded `/api` to `http://localhost:5000`
   - The .NET API actually runs on `http://localhost:5098` (from `launchSettings.json`)
   - Fix: updated `target` to `http://localhost:5098`

2. **HTTPS redirect in development**
   - `app.UseHttpsRedirection()` was called unconditionally
   - In development, Vite proxy sends plain HTTP, which got a 307 redirect to HTTPS and then failed
   - Fix: wrapped in `if (!app.Environment.IsDevelopment())`

```typescript
// vite.config.ts — before
proxy: { '/api': { target: 'http://localhost:5000' } }

// vite.config.ts — after
proxy: { '/api': { target: 'http://localhost:5098', changeOrigin: true } }
```

```csharp
// Program.cs — before
app.UseHttpsRedirection();

// Program.cs — after
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
```

---

## Phase 3 — Open Library Returning 0 Results

**Problem:** Every search returned 0 book candidates. Logs showed `InvalidOperationException: An invalid request URI was provided. The request URI must either be an absolute URI or BaseAddress must be set.`

**Root cause:** Incorrect DI registration of `OpenLibraryClient`.

The original registration was:
```csharp
// BROKEN — two separate registrations
services.AddHttpClient<OpenLibraryClient>();          // typed client with BaseAddress
services.AddScoped<IBookRepository, OpenLibraryClient>(); // generic scoped — gets a plain HttpClient with no BaseAddress
```

When `IBookRepository` was resolved, .NET's DI used the second (scoped) registration, which created a plain `HttpClient` with no `BaseAddress`. The typed client with the configured base URL was never used.

**Fix:** Single registration linking the interface to the implementation:
```csharp
// FIXED — one registration, interface → implementation, with BaseAddress
services.AddHttpClient<IBookRepository, OpenLibraryClient>(client =>
{
    client.BaseAddress = new Uri(olOptions.BaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(olOptions.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("User-Agent", "FindThatBook/1.0 (...)");
});
```

---

## Phase 4 — Gemini Integration Fixes

### Fix 4a — Rate limiting (429) and silent errors

**Problem:** Gemini returned 429 errors that were caught and swallowed with no useful log message, making it impossible to diagnose.

**Fix:** Added explicit error body logging before falling back:
```csharp
if (!response.IsSuccessStatusCode)
{
    var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
    _logger.LogWarning("Gemini returned {StatusCode}. Body: {Body}. Falling back to regex.",
        (int)response.StatusCode, errorBody);
    return await _fallback.ExtractAsync(rawQuery, cancellationToken);
}
```

### Fix 4b — `responseMimeType` not supported

**Problem:** The initial `generationConfig` included `responseMimeType = "application/json"`. This parameter is not supported on all Gemini free-tier API keys/regions and caused requests to fail silently.

**Fix:** Removed `responseMimeType` from the request body entirely.

### Fix 4c — JSON extraction from Gemini response

**Problem:** Gemini sometimes wraps its output in markdown code fences (` ```json … ``` `) even when instructed not to. The raw text failed `JsonDocument.Parse`.

**Fix:** Added cleanup pipeline in `ParseGeminiResponse`:
```csharp
// Strip markdown fences if model ignored instructions
var cleaned = Regex.Replace(text.Trim(), @"^```(?:json)?\s*|\s*```$", "", RegexOptions.Multiline).Trim();

// Find first JSON object (handles any preamble text)
var start = cleaned.IndexOf('{');
var end = cleaned.LastIndexOf('}');
if (start >= 0 && end > start)
    cleaned = cleaned[start..(end + 1)];
```

### Fix 4d — Free-tier quota limit of zero

**Problem:** Both API keys had `limit: 0` on all free-tier quota metrics. This is not "quota exhausted" — it is quota never allocated. It happens when keys are created from Google Cloud Console projects that don't have free-tier Gemini access enabled.

**Diagnosis:** Direct `curl` against the Gemini API confirmed:
```json
"violations": [
  { "quotaId": "GenerateRequestsPerDayPerProjectPerModel-FreeTier", "limit": 0 }
]
```

**Resolution:** Generated a new API key from [Google AI Studio](https://aistudio.google.com) (not Google Cloud Console). AI Studio keys receive automatic free-tier quota (1500 req/day on `gemini-2.0-flash`).

### Fix 4e — `gemini-2.0-flash` deprecated for new keys

**Problem:** After switching to a fresh AI Studio key, `gemini-2.0-flash` returned:
```json
{ "code": 404, "message": "This model models/gemini-2.0-flash is no longer available to new users." }
```

**Fix:** Switched model to `gemini-2.5-flash`, which is the current recommended model and verified working:
```json
{ "Gemini": { "Model": "gemini-2.5-flash" } }
```

### Fix 4f — Truncated JSON from thinking model

**Problem:** `gemini-2.5-flash` is a "thinking" model that uses internal reasoning tokens counted against `maxOutputTokens`. With a limit of 256, the JSON response was cut off mid-string:

```
{"title":"Adventures of Huckleberry Finn","author":"Mark Twain","keywords":[],"year":null,"confidence":"high","reasoning":"
```

This caused `JsonException: Expected end of string, but instead reached end of data` in `ParseGeminiResponse`.

**Fix:** Two changes to `generationConfig`:
1. Disabled thinking with `thinkingBudget: 0` — extraction is deterministic, no chain-of-thought needed
2. Increased `maxOutputTokens` from 256 to 512 — extra headroom

```csharp
generationConfig = new
{
    temperature = 0.1,
    maxOutputTokens = 512,
    thinkingConfig = new { thinkingBudget = 0 }
}
```

---

## Phase 5 — Compiler Errors Fixed

### CS0104 — Ambiguous `MatchType`

**Problem:** `System.IO.MatchType` (introduced in .NET 5) conflicted with `FindThatBook.Domain.Enums.MatchType`, causing `CS0104: 'MatchType' is an ambiguous reference` in any file that had both `using System.IO` and `using FindThatBook.Domain.Enums`.

**Fix:** Added a type alias in affected files:
```csharp
using DomainMatchType = FindThatBook.Domain.Enums.MatchType;
```
Applied to: `BookMatchingService.cs`, `MatchedBook.cs`, `BookMatchingServiceTests.cs`.

### `ExtractionResult.Empty` missing parameters

**Problem:** `ExtractionResult.Empty` was created without the `Year` and `Reasoning` parameters added in a later refactor, causing a compile error.

**Fix:**
```csharp
public static ExtractionResult Empty =>
    new(null, null, [], null, "low", ExtractionMethod.RegexFallback, "No fields extracted.");
```

### `RegexFallbackExtractor` — `Concat2Iterator.ToArray()` crash

**Problem:** `noiseKeywords.Concat(meaningfulTokens.Skip(4)).ToArray()` produced a `Concat2Iterator` that threw at runtime in some edge cases.

**Fix:** Replaced with explicit `List<string>` and `GetRange()`:
```csharp
private static string[] BuildKeywords(List<string> noiseKeywords, List<string> meaningfulTokens)
{
    var extra = meaningfulTokens.Count > 4
        ? meaningfulTokens.GetRange(4, meaningfulTokens.Count - 4)
        : new List<string>();
    var result = new List<string>(noiseKeywords);
    result.AddRange(extra);
    return result.ToArray();
}
```

### `StripSubtitle` returning wrong result for `", or "`

**Problem:** `TextNormalizer.StripSubtitle("The Hobbit, or There and Back Again")` was returning `"The Hobbit,"` instead of `"The Hobbit"` because the separator `" or "` was checked before `", or "`, matching mid-string instead of at the separator.

**Fix:** Reordered the separator array to check longer/more-specific patterns first:
```csharp
private static readonly string[] SubtitleSeparators = { ", or ", " or ", ": ", " - " };
```

### Missing `Microsoft.Extensions.Logging.Abstractions` reference

**Problem:** `SearchBooksUseCase` used `ILogger<T>` but the `Application` project didn't reference the logging abstractions package.

**Fix:** Added NuGet package to `FindThatBook.Application.csproj`:
```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
```
