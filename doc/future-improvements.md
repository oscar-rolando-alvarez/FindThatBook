# Future Improvements

Opportunities to improve performance, reduce costs, and extend the system. Each item includes the problem it solves, effort estimate, and expected impact.

---

## Cost Reduction

### 1. Cache Gemini Responses (High Impact / Low Effort)

**Problem:** Every query triggers a Gemini API call, even for identical or near-identical inputs. Each call consumes tokens and counts against daily quota.

**Solution:** Add an in-memory or distributed cache keyed on the normalized query string.

```csharp
// In GeminiExtractor or a caching decorator
public class CachingAiExtractor : IAiExtractor
{
    private readonly IAiExtractor _inner;
    private readonly IMemoryCache _cache;

    public async Task<ExtractionResult> ExtractAsync(string rawQuery, CancellationToken ct = default)
    {
        var key = $"gemini:{rawQuery.ToLowerInvariant().Trim()}";
        if (_cache.TryGetValue(key, out ExtractionResult cached))
            return cached;

        var result = await _inner.ExtractAsync(rawQuery, ct);
        _cache.Set(key, result, TimeSpan.FromHours(24));
        return result;
    }
}
```

**Impact:** Eliminates duplicate API calls for popular queries. Given that book queries cluster heavily (Harry Potter, Lord of the Rings, etc.), a cache hit rate of 40–60% is realistic within hours of launch.

**Effort:** ~2 hours. Register `IMemoryCache` in DI, wrap `GeminiExtractor` with a decorator.

---

### 2. Cache Open Library Search Results

**Problem:** Open Library has no rate limiting but is a public API with shared capacity. Repeated identical searches add unnecessary latency and load.

**Solution:** Cache `SearchAsync` results by a hash of title + author + keywords.

```csharp
var cacheKey = $"ol:{extraction.Title}:{extraction.Author}:{string.Join(",", extraction.Keywords)}";
```

**Impact:** Eliminates redundant HTTP round-trips (typically 2–4 requests per search). Particularly effective if the frontend retries on navigation events.

**Effort:** ~2 hours. Same decorator pattern as AI caching.

---

### 3. Switch to `gemini-2.0-flash-lite` for Extraction

**Problem:** `gemini-2.5-flash` is a thinking model — even with `thinkingBudget: 0`, it has higher base cost per token than non-thinking models.

**Solution:** Use `gemini-2.0-flash-lite` for the extraction task (simple structured JSON from short text). Reserve `gemini-2.5-flash` only for ambiguous queries that the lighter model returns `"confidence": "low"` on.

**Two-tier strategy:**
1. Try `gemini-2.0-flash-lite` first
2. If `confidence == "low"`, escalate to `gemini-2.5-flash`

**Impact:** ~80% of queries are unambiguous (clear title/author). The cheaper model handles most of them at a fraction of the cost.

**Effort:** ~3 hours. Add a second `IAiExtractor` registration and a routing layer in the use case.

---

### 4. Reduce Open Library Search Results from 20 to 10

**Problem:** `MaxSearchResults: 20` fetches up to 20 candidates per search strategy, with 5 strategies possible. In the worst case this is 100 candidates de-duplicated to 20 — most of which the matching engine discards anyway.

**Solution:** Lower `MaxSearchResults` to 10. The matching engine already limits final results to 5. Fetching more than 10–12 candidates provides diminishing returns on result quality.

**Impact:** Halves the payload size from Open Library and reduces matching CPU work.

**Effort:** 5 minutes. Change `appsettings.json`:
```json
"OpenLibrary": { "MaxSearchResults": 10 }
```

---

## Performance

### 5. Parallel Search Strategies in `OpenLibraryClient`

**Problem:** The 5 search strategies execute sequentially. Strategies 2–5 only run if earlier ones return few results, but strategies 1 and 2 (title+author, title-only) could safely run in parallel since both are always triggered when a title is present.

**Solution:** Run independent strategies concurrently with `Task.WhenAll`:

```csharp
var tasks = new List<Task<List<SearchDoc>>>();

if (hasTitle && hasAuthor)
    tasks.Add(SearchByTitleAndAuthorAsync(title, author, ct));
if (hasTitle)
    tasks.Add(SearchByQueryAsync(title, ct));

var results = await Task.WhenAll(tasks);
foreach (var docs in results)
    MergeDocs(allDocs, docs);

// Conditional strategies after initial results are known
if (hasAuthor && allDocs.Count < 5)
    MergeDocs(allDocs, await SearchByQueryAsync(author, ct));
```

**Impact:** Reduces perceived latency by ~40–60% for queries with both title and author. Two HTTP calls in ~300ms instead of two sequential calls in ~600ms.

**Effort:** ~2 hours. Refactor `SearchAsync` to use `Task.WhenAll` for independent strategies.

---

### 6. Response Streaming

**Problem:** The API waits for the full pipeline (AI extraction + book search + ranking) before sending any response. For slow Gemini responses (~1–2s) or slow Open Library calls, the user sees nothing for 2–3 seconds.

**Solution:** Stream results using Server-Sent Events (SSE) or chunked JSON:
1. Send extraction result immediately after Gemini responds
2. Stream book candidates as they arrive from Open Library
3. Frontend progressively renders results

**Impact:** Dramatically improves perceived responsiveness. The extraction panel appears in ~500ms instead of ~2000ms.

**Effort:** ~1 week. Requires SSE or WebSocket support in the API, and a streaming-aware frontend using `ReadableStream` or EventSource.

---

### 7. Smarter Fallback Ordering

**Problem:** `RegexFallbackExtractor` always returns `confidence: "low"` and cannot distinguish author from title. For clearly formatted queries (`"tolkien hobbit"`), regex works fine. For ambiguous ones (`"bennet austen"`), it fails.

**Solution:** Add a confidence-aware routing layer:
- If Gemini returns `"high"` or `"medium"` → use result
- If Gemini returns `"low"` → retry with a simplified prompt on `gemini-2.0-flash-lite`
- If both return `"low"` → fall back to regex and widen the Open Library search

**Impact:** Better results for edge cases without always paying for the expensive model.

**Effort:** ~4 hours. Add confidence check in `SearchBooksUseCase` and a retry path.

---

## Quality & Reliability

### 8. Distributed Caching with Redis

**Problem:** `IMemoryCache` (in-process cache) doesn't survive restarts and doesn't scale across multiple API instances.

**Solution:** Replace `IMemoryCache` with `IDistributedCache` backed by Redis.

```csharp
services.AddStackExchangeRedisCache(options =>
    options.Configuration = configuration.GetConnectionString("Redis"));
```

**Impact:** Cache survives deployments and scales horizontally. Especially valuable if deployed to a container orchestrator.

**Effort:** ~4 hours. Introduce Redis connection, update cache decorators to serialize/deserialize.

---

### 9. Rate Limiting and Request Queuing

**Problem:** No rate limiting on the API. A burst of requests can exhaust the Gemini daily quota within minutes.

**Solution:**
- ASP.NET Core rate limiting middleware (`.NET 7+`):
  ```csharp
  builder.Services.AddRateLimiter(options =>
      options.AddFixedWindowLimiter("gemini", o =>
      {
          o.PermitLimit = 10;
          o.Window = TimeSpan.FromMinutes(1);
      }));
  ```
- A per-IP concurrency limit to prevent individual clients from consuming all quota

**Impact:** Protects Gemini quota from abuse or accidental hammering from the frontend.

**Effort:** ~3 hours.

---

### 10. Integration Tests Against Real Open Library API

**Problem:** The test suite is entirely unit tests with mocked repositories. Bugs in the `OpenLibraryClient` JSON deserialization (e.g., API response shape changes) won't be caught until production.

**Solution:** Add integration tests in the empty `tests/FindThatBook.Tests/Integration/` folder that make real HTTP calls to Open Library in a controlled test environment:

```csharp
[Fact]
public async Task SearchAsync_ForHobbit_ReturnsAtLeastOneResult()
{
    var client = new WebApplicationFactory<Program>().CreateClient();
    var response = await client.PostAsJsonAsync("/api/v1/books/search",
        new { query = "the hobbit tolkien", maxResults = 5 });
    response.EnsureSuccessStatusCode();
    var body = await response.Content.ReadFromJsonAsync<BookSearchResponse>();
    body!.Results.Should().NotBeEmpty();
}
```

**Impact:** Catches real-world API contract changes and DI wiring bugs before users see them.

**Effort:** ~4 hours. Uses `WebApplicationFactory<Program>` (already hooked up in `Program.cs`).

---

### 11. OpenTelemetry Tracing

**Problem:** When a request is slow, it's unclear whether the bottleneck is Gemini, Open Library, or the matching engine. Log-level timing exists (`ProcessingTimeMs` in the response) but is coarse.

**Solution:** Add OpenTelemetry tracing with spans for each pipeline stage:
- `gemini.extract` span
- `openlibrary.search` span (with strategy sub-spans)
- `matching.score_and_rank` span

```csharp
services.AddOpenTelemetry()
    .WithTracing(b => b
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("FindThatBook.*")
        .AddOtlpExporter());
```

**Impact:** Pinpoints latency sources per request. Essential for production debugging.

**Effort:** ~1 day.

---

### 12. Alternative AI Provider Support

**Problem:** The system is tightly bound to Gemini. If Gemini quota runs out or the service has an outage, all structured extraction degrades to regex.

**Solution:** Implement `IAiExtractor` for a second provider (e.g., Groq with `llama-3.1-8b-instant`) and add a failover chain:

```
GeminiExtractor → GroqExtractor → RegexFallbackExtractor
```

Groq offers a generous free tier (14,400 req/day on llama-3.1-8b) with very low latency (~200ms). The extraction prompt translates directly since both providers use the same OpenAI-compatible message format.

**Impact:** Near-zero downtime even when Gemini is unavailable or quota-limited.

**Effort:** ~4 hours. `GroqExtractor` implementation is nearly identical to `GeminiExtractor` — Groq uses the OpenAI API format.

---

## Feature Enhancements

### 13. Query History and Favourites

**Problem:** Users can't return to previous searches or bookmark books they found.

**Solution:** Browser `localStorage` for client-side history (no backend needed):
- Recent 10 queries shown as chips under the search bar
- Favourite button on `ResultCard` persists to `localStorage`

**Effort:** ~1 day (frontend only).

---

### 14. Edition-Level Details Panel

**Problem:** `BookDetailResponse` returns work-level data but not edition-specific details (ISBN, publisher, page count, language).

**Solution:** Fetch `/works/{id}/editions.json` from Open Library and display editions in a collapsible panel within `ResultCard`.

**Effort:** ~4 hours. Extend `OpenLibraryClient.GetWorkDetailsAsync` and add a frontend accordion component.

---

### 15. Subject / Genre Facets

**Problem:** Search results can't be filtered. A search for "dickens" returns all Dickens works with no way to narrow to, say, novels vs. short stories.

**Solution:** Aggregate `Subjects` from all result work records, display as clickable filter chips, and re-rank locally to surface matching subjects first.

**Effort:** ~1 day.

---

## Priority Matrix

| # | Improvement | Impact | Effort | Priority |
|---|---|---|---|---|
| 1 | Cache Gemini responses | High | Low | **Do first** |
| 2 | Cache Open Library results | Medium | Low | **Do first** |
| 5 | Parallel search strategies | High | Low | **Do first** |
| 3 | Lighter model for easy queries | High | Medium | Next sprint |
| 9 | Rate limiting | High | Medium | Next sprint |
| 4 | Reduce MaxSearchResults | Medium | Trivial | Now (config change) |
| 10 | Integration tests | High | Medium | Next sprint |
| 12 | Alternative AI provider (Groq) | High | Medium | Next sprint |
| 6 | Response streaming | High | High | Later |
| 11 | OpenTelemetry tracing | Medium | Medium | Later |
| 8 | Redis distributed cache | Medium | Medium | Later (if scaling) |
| 7 | Smarter fallback ordering | Medium | Medium | Later |
| 13 | Query history (localStorage) | Low | Low | Nice to have |
| 14 | Edition details panel | Low | Medium | Nice to have |
| 15 | Subject facets | Medium | Medium | Nice to have |
