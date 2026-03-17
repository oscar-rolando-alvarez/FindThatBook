# Find That Book

AI-powered library discovery workflow — Technical Challenge
**Author:** Oscar Alvarez | **Date:** March 2026

---

## Overview

Given a messy plain-text query (e.g. `"tolkien hobbit illustrated deluxe 1937"`, `"mark huckleberry"`, `"austen bennet"`), Find That Book resolves it to ranked book candidates from [Open Library](https://openlibrary.org), using Google Gemini AI for intelligent field extraction.

---

## Quick Start

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (targets net10.0)
- [Node.js 20+](https://nodejs.org)
- A **Gemini API key** — get one free at [ai.google.dev](https://ai.google.dev/gemini-api/docs/api-key)

### 1. Clone & Configure API Key

```bash
git clone <repo-url>
cd find-that-book
```

Edit `src/FindThatBook.Api/appsettings.json` and set your Gemini key:

```json
{
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY"
  }
}
```

> The app **works without a Gemini key** — it falls back to regex-based extraction automatically.

### 2. Run the API

```bash
dotnet run --project src/FindThatBook.Api
# API starts at http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

### 3. Run the Frontend

```bash
cd client
npm install
npm run dev
# Opens at http://localhost:5173
```

### 4. Run Tests

```bash
dotnet test
# 43 tests: 0 failed
```

---

## Architecture

Clean Architecture with 5 layers (dependency flow: API → Application → Domain ← Infrastructure):

```
FindThatBook/
├── src/
│   ├── FindThatBook.Domain/          # Entities, value objects, matching logic (zero deps)
│   │   ├── Entities/                 # SearchQuery, BookCandidate, MatchedBook
│   │   ├── ValueObjects/             # ExtractionResult, AuthorInfo
│   │   ├── Enums/                    # MatchType, ExtractionMethod
│   │   └── Services/                 # BookMatchingService, TextNormalizer
│   ├── FindThatBook.Application/     # Use cases, DTOs, interfaces
│   │   ├── UseCases/                 # SearchBooksUseCase (orchestrator)
│   │   ├── DTOs/                     # BookSearchRequest/Response
│   │   ├── Interfaces/               # IAiExtractor, IBookRepository
│   │   └── Common/                   # Result<T> pattern
│   ├── FindThatBook.Infrastructure/  # External services
│   │   ├── AI/                       # GeminiExtractor, RegexFallbackExtractor
│   │   ├── OpenLibrary/              # OpenLibraryClient
│   │   └── Options/                  # GeminiOptions, OpenLibraryOptions
│   └── FindThatBook.Api/             # ASP.NET Core Web API
│       ├── Controllers/              # BookSearchController, HealthController
│       └── Middleware/               # GlobalExceptionMiddleware
├── tests/
│   └── FindThatBook.Tests/
│       └── Unit/                     # 43 unit tests
└── client/                           # React 18 + TypeScript frontend
```

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/v1/books/search` | Search for books by messy query |
| `GET`  | `/api/v1/books/{workId}` | Get full details for a work |
| `GET`  | `/api/v1/health` | Health check |

### Example Request

```bash
curl -X POST http://localhost:5000/api/v1/books/search \
  -H "Content-Type: application/json" \
  -d '{"query": "tolkien hobbit illustrated deluxe 1937", "maxResults": 5}'
```

### Example Response

```json
{
  "query": "tolkien hobbit illustrated deluxe 1937",
  "extraction": {
    "title": "The Hobbit",
    "author": "J.R.R. Tolkien",
    "keywords": ["illustrated", "deluxe"],
    "year": 1937,
    "confidence": "high",
    "method": "gemini"
  },
  "results": [
    {
      "title": "The Hobbit",
      "author": "J.R.R. Tolkien",
      "firstPublishYear": 1937,
      "openLibraryWorkId": "/works/OL45804W",
      "openLibraryUrl": "https://openlibrary.org/works/OL45804W",
      "coverImageUrl": "https://covers.openlibrary.org/b/id/8406786-M.jpg",
      "matchRank": 1,
      "matchType": "ExactTitlePrimaryAuthor",
      "explanation": "Exact title match; J.R.R. Tolkien is primary author."
    }
  ],
  "totalCandidates": 18,
  "processingTimeMs": 1247
}
```

---

## Implementation Details

### AI Integration — Google Gemini

The system uses `gemini-2.0-flash` for structured field extraction from messy queries. The prompt includes few-shot examples for character-to-book mapping (e.g. `"mark huckleberry"` → `{author: "Mark Twain", title: "Huckleberry Finn"}`).

**Fallback chain:**
1. Try Gemini AI (8s timeout)
2. On failure → regex-based heuristics
3. Always proceed to Open Library search with best-effort fields

The API key is kept **server-side only** and never exposed to the frontend.

### Matching Hierarchy (5-Tier)

| Tier | Type | Score | Logic |
|------|------|-------|-------|
| 1 | `ExactTitlePrimaryAuthor` | 100 | Exact normalized title + primary author |
| 2 | `ExactTitleContributorAuthor` | 80 | Exact title, author is contributor/editor |
| 3 | `NearMatchTitleAuthor` | 60 | Fuzzy title (Levenshtein ≤ 3) + author |
| 4 | `AuthorOnlyFallback` | 40 | No title — return top works by author |
| 5 | `KeywordCandidate` | 20 | 2+ keyword overlaps |

### Text Normalization Pipeline

`lowercase → diacritics removal → punctuation strip → whitespace collapse → noise word removal → subtitle handling`

Handles: `"The Hobbit, or There and Back Again"` ↔ `"The Hobbit"`, diacritics (`é → e`), partial names (`tolkien → J.R.R. Tolkien` via AI).

### Multi-Strategy Open Library Search

For each query, up to 4 searches run:
1. `title + author` (most precise)
2. `title` only
3. `author` only (if few results)
4. `keywords` (last resort)

Results are de-duplicated by `work_id`.

### Error Handling

- `Result<T>` pattern — no exceptions for business logic
- Global `GlobalExceptionMiddleware` → RFC 7807 `ProblemDetails` with correlation ID
- Open Library: 5s timeout, graceful error returns
- Gemini: 8s timeout → regex fallback (always returns 200)

---

## Design Decisions

| Decision | Choice | Why |
|----------|--------|-----|
| AI Provider | Google Gemini (free tier) | Per requirements; free API key |
| Architecture | Clean Architecture (5 layers) | Testability, separation of concerns |
| AI Fallback | Regex-based extractor | Service never fails completely |
| Matching | Levenshtein + normalization | No external deps; handles typos |
| Error contract | `Result<T>` + ProblemDetails | No exception-driven control flow |
| HTTP resilience | Built-in HttpClient timeout | Simple, no extra libs needed |

**Why not MediatR?** The challenge scope doesn't require it — `SearchBooksUseCase` is injected directly, keeping dependencies minimal and the flow transparent.

---

## Testing Strategy

43 unit tests across 4 test classes:

| Class | What it tests |
|-------|--------------|
| `TextNormalizerTests` | Normalization, diacritics, subtitle stripping, Levenshtein |
| `BookMatchingServiceTests` | All 5 tiers, subtitle variants, ranking order, deduplication |
| `RegexFallbackExtractorTests` | Year detection, token extraction, edge cases |
| `SearchBooksUseCaseTests` | Full pipeline (mocked Gemini + repo), timeout handling, empty results |

Run: `dotnet test`

---

## Future Improvements

| Category | Enhancement | Impact |
|----------|-------------|--------|
| Performance | `Task.WhenAll` for parallel Open Library calls | 30% faster |
| Performance | Redis cache for Open Library responses (TTL: 1h) | 50%+ faster repeat queries |
| AI | AI re-ranking pass on top 10 candidates | Better result ordering |
| AI | Embedding-based similarity for re-ranking | More nuanced matching |
| UX | Search history + autocomplete | Faster repeat searches |
| Infrastructure | Rate limiting + API key management | Production-ready security |
| Infrastructure | Structured logging with Serilog + Seq | Observability |
| Testing | Integration tests with `WebApplicationFactory` | Full pipeline coverage |
| Testing | Property-based testing with FsCheck | Edge case coverage |
| Deployment | Docker Compose + Railway/Azure deployment | Live demo |

---

## Tech Stack

**Backend:** .NET 10 · ASP.NET Core Web API · Clean Architecture
**AI:** Google Gemini 2.0 Flash (with regex fallback)
**Data:** Open Library public API (no API key needed)
**Frontend:** React 18 · TypeScript · Vite · TanStack Query
**Testing:** xUnit · Moq · FluentAssertions
**Docs:** Swagger / OpenAPI (available at `/swagger`)

---

*Technical Challenge — Oscar Alvarez — March 2026*
