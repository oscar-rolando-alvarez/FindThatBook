# Find That Book — Documentation Index

> AI-powered book discovery: submit a messy query, get intelligently ranked results from Open Library.

---

## Contents

| Document | Description |
|---|---|
| [architecture.md](./architecture.md) | System design, layer breakdown, data flow, algorithms, and API reference |
| [changes.md](./changes.md) | Full history of what was built and every significant fix applied |
| [future-improvements.md](./future-improvements.md) | Roadmap for performance gains and cost reduction |

---

## Quick Start

```bash
# Backend (http://localhost:5098)
dotnet run --project src/FindThatBook.Api

# Frontend (http://localhost:5173)
cd client && npm run dev

# Tests
dotnet test tests/FindThatBook.Tests/
```

## Tech Stack at a Glance

| Layer | Technology |
|---|---|
| API | ASP.NET Core (.NET 10), Clean Architecture |
| AI Extraction | Google Gemini 2.5 Flash |
| Book Data | Open Library public API |
| Frontend | React 19, TypeScript, Vite 5, TanStack Query |
| Tests | xUnit, Moq, FluentAssertions (43 tests) |
