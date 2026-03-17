# Architecture

## Overview

Find That Book is a full-stack book discovery application built on Clean Architecture principles. A user submits any freeform query — even a vague or misspelled one — and the system uses AI to understand intent, searches Open Library, scores candidates with a domain matching engine, and returns ranked results.

```
┌─────────────────────────────────────────────────────────────────┐
│                        React Frontend                           │
│  SearchBar → ExtractionPanel → ResultCards → LoadingSkeleton    │
│  TanStack React Query · Axios · Vite Dev Proxy (:5173 → :5098)  │
└─────────────────────────────┬───────────────────────────────────┘
                              │  POST /api/v1/books/search
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                          API Layer                              │
│  BookSearchController · HealthController                        │
│  GlobalExceptionMiddleware · RFC 7807 ProblemDetails            │
│  Swagger/OpenAPI · CORS (AllowAnyOrigin in dev)                 │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      Application Layer                          │
│  SearchBooksUseCase (orchestrator)                              │
│  Result<T> (railway-oriented error handling)                    │
│  IAiExtractor · IBookRepository (port interfaces)              │
└──────────────┬──────────────────────────────┬───────────────────┘
               │                              │
               ▼                              ▼
┌──────────────────────────┐    ┌─────────────────────────────────┐
│    Infrastructure: AI    │    │   Infrastructure: OpenLibrary    │
│  GeminiExtractor         │    │  OpenLibraryClient               │
│    ↓ (on any failure)    │    │  5-strategy search               │
│  RegexFallbackExtractor  │    │  De-duplication by work_id       │
└──────────────────────────┘    └─────────────────────────────────┘
               │                              │
               └──────────────┬───────────────┘
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        Domain Layer                             │
│  BookMatchingService (5-tier scoring)                           │
│  TextNormalizer (normalization + Levenshtein)                   │
│  Entities: SearchQuery, BookCandidate, MatchedBook              │
│  Value Objects: ExtractionResult, AuthorInfo                    │
│  Enums: MatchType, ExtractionMethod                             │
└─────────────────────────────────────────────────────────────────┘
```

---

## Layer Breakdown

### Domain Layer (`FindThatBook.Domain`)

The innermost layer. Zero external dependencies — pure C# with no framework references.

**Entities**

| Class | Responsibility |
|---|---|
| `SearchQuery` | Encapsulates the raw user input and the AI-extracted `ExtractionResult` |
| `BookCandidate` | A raw result from Open Library: title, authors, year, work ID, cover IDs |
| `MatchedBook` | A scored candidate: wraps `BookCandidate` with rank, `MatchType`, score, and explanation |

**Value Objects**

| Record | Fields |
|---|---|
| `ExtractionResult` | `Title?`, `Author?`, `Keywords[]`, `Year?`, `Confidence`, `Method`, `Reasoning` |
| `AuthorInfo` | `Name`, `Key` (OL path), `Role` — `IsPrimaryAuthor` distinguishes authors from editors/illustrators |

**Domain Services**

`BookMatchingService` — stateless scoring engine:
1. For each `BookCandidate`, compare its title and author against the `ExtractionResult`
2. Determine a `MatchType` using the 5-tier hierarchy
3. Sort by score descending, then `FirstPublishYear` ascending (canonical editions surface first)
4. Trim to `maxResults` and assign `MatchRank` (1–N)

`TextNormalizer` — static utility pipeline:

```
Input: "The Hobbit: Or There and Back Again"
  → lowercase        → "the hobbit: or there and back again"
  → diacritics       → (unchanged)
  → strip punctuation → "the hobbit or there and back again"
  → strip subtitle   → "the hobbit"
  → noise words      → "hobbit"
```

---

### Application Layer (`FindThatBook.Application`)

Orchestration only. References domain types and defines port interfaces. No framework or HTTP dependencies.

**`SearchBooksUseCase` pipeline:**

```
1. Validate → reject empty queries (Result.Failure 400)
2. Extract  → IAiExtractor.ExtractAsync(rawQuery)
3. Search   → IBookRepository.SearchAsync(extraction)
4. Rank     → BookMatchingService.ScoreAndRank(query, candidates, maxResults)
5. Map      → Build BookSearchResponse with extraction details + ranked results + timing
6. Return   → Result.Success(response)
```

Error mapping:
- `OperationCanceledException` / `TimeoutException` → 408
- `HttpRequestException` → 503
- Unhandled → `GlobalExceptionMiddleware` → 500 with correlation ID

**`Result<T>`** (railway-oriented):
```csharp
Result<T>.Success(value)         // IsSuccess = true, Value = value
Result<T>.Failure(msg, code)     // IsSuccess = false, Error = msg, ErrorCode = code
```

---

### Infrastructure Layer (`FindThatBook.Infrastructure`)

Adapters that implement the domain interfaces against external systems.

#### AI Extraction

**`GeminiExtractor`** calls Google Gemini (`gemini-2.5-flash`) with a carefully crafted prompt:

```
You are a librarian assistant. Given a messy text blob...
- "mark huckleberry" → author "Mark Twain", title "Adventures of Huckleberry Finn"
- "austen bennet"    → author "Jane Austen",  title "Pride and Prejudice"
- "tolkien"          → "J.R.R. Tolkien"
...
Return ONLY valid JSON: {"title":..., "author":..., "keywords":[...], "year":..., "confidence":..., "reasoning":...}
```

Key configuration:
- `temperature: 0.1` — near-deterministic output
- `maxOutputTokens: 512` — sufficient for JSON response
- `thinkingBudget: 0` — disables chain-of-thought (not needed for extraction; saves tokens and latency)

Falls back to `RegexFallbackExtractor` on: non-2xx HTTP, timeout, empty response, JSON parse error, or unconfigured API key.

**`RegexFallbackExtractor`** — heuristic extraction:
- Detects 4-digit years (1600–2029)
- Tokenizes, separates noise words (`illustrated`, `deluxe`, `edition`, …) from meaningful tokens
- First 4 meaningful tokens become the title guess
- Always returns `Confidence = "low"` and `Method = RegexFallback`

#### Book Search

**`OpenLibraryClient`** executes up to 5 strategies and de-duplicates by `work_id`:

| Strategy | Condition | Query |
|---|---|---|
| 1 | Title + Author present | `?title=…&author=…` |
| 2 | Title present | `?q=…` (title only) |
| 3 | Author present and < 5 results | `?q=…` (author only) |
| 4 | Keywords present and < 3 results | `?q=…` (top 3 keywords) |
| 5 | Still empty | `?q=…` (title broad fallback) |

`GetWorkDetailsAsync` fetches `/works/{id}.json`, resolves up to 5 authors via their individual author records, extracts description (handles both string and `{value: "…"}` JSON shapes), and parses first publish year from free-text date strings.

---

### API Layer (`FindThatBook.Api`)

Thin HTTP shell. Controllers translate HTTP concerns into use case calls and map results to status codes.

**Endpoints:**

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/books/search` | Main search — body: `BookSearchRequest` |
| `GET` | `/api/v1/books/{workId}` | Book detail by Open Library work ID |
| `GET` | `/api/v1/health` | Liveness probe |
| `GET` | `/swagger` | OpenAPI UI (dev only) |

**HTTP status mapping:**

| Scenario | Status |
|---|---|
| Success | 200 |
| Empty/invalid query | 400 |
| Upstream timeout | 408 |
| Open Library unreachable | 503 |
| Unhandled exception | 500 + correlation ID |

---

### Frontend (`client/`)

React 19 single-page app. Communicates with the backend via Vite's dev proxy (`/api` → `http://localhost:5098`).

**Component tree:**
```
App (QueryClientProvider + useMutation)
├── SearchBar        — text input, example buttons, Enter key
├── LoadingSkeleton  — 3 placeholder cards while pending
├── ExtractionPanel  — extracted title/author/keywords/confidence/method/reasoning
└── ResultCard[]     — cover, title, author, year, match badge, explanation, OL link
```

**Data flow:**
```
User types → SearchBar.onSearch(query)
  → useMutation → searchBooks(request)   [Axios POST]
  → BookSearchResponse
  → ExtractionPanel + ResultCard × N
```

---

## 5-Tier Matching Hierarchy

| Tier | MatchType | Score | Condition |
|---|---|---|---|
| 1 | `ExactTitlePrimaryAuthor` | 100 | Title matches exactly + first-listed author matches |
| 2 | `ExactTitleContributorAuthor` | 80 | Title matches exactly + contributor matches (or no author in query) |
| 3 | `NearMatchTitleAuthor` | 60 | Title fuzzy-matches (Levenshtein ≤ 3) + any author matches |
| 4 | `AuthorOnlyFallback` | 40 | Author matches, no title in query |
| 5 | `KeywordCandidate` | 20 | ≥ 2 keywords found in title/subjects |

Tie-breaking: lower `FirstPublishYear` wins (canonical first editions surface above reprints).

---

## Configuration Reference

**`appsettings.json`**

```json
{
  "Gemini": {
    "ApiKey": "...",
    "Model": "gemini-2.5-flash",
    "TimeoutSeconds": 8
  },
  "OpenLibrary": {
    "BaseUrl": "https://openlibrary.org",
    "TimeoutSeconds": 5,
    "MaxSearchResults": 20
  }
}
```

All values are bound via `IOptions<T>` and validated at startup. API key detection: if the value equals `"YOUR_GEMINI_API_KEY"` or is empty, Gemini is bypassed and regex fallback is used directly.

---

## Dependency Injection Map

```
Singleton  BookMatchingService
Singleton  RegexFallbackExtractor
HttpClient GeminiExtractor            → Scoped IAiExtractor
HttpClient OpenLibraryClient          → Scoped IBookRepository
  └── BaseAddress = https://openlibrary.org/
  └── Timeout = 5s
  └── User-Agent: FindThatBook/1.0
Scoped     SearchBooksUseCase
```

---

## Test Coverage

| File | Tests | What it covers |
|---|---|---|
| `TextNormalizerTests.cs` | 12 | Normalization pipeline, subtitle stripping, Levenshtein, fuzzy threshold |
| `BookMatchingServiceTests.cs` | 14 | All 5 tiers, fuzzy matching, rank ordering, subtitle variants, max results |
| `RegexFallbackExtractorTests.cs` | 9 | Year detection, tokenization, confidence always low, keywords |
| `SearchBooksUseCaseTests.cs` | 8 | Pipeline orchestration, error codes (400/408/503), empty results |
| **Total** | **43** | All pass |
