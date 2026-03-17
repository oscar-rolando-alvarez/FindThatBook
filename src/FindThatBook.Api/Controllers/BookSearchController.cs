using FindThatBook.Application.DTOs;
using FindThatBook.Application.Interfaces;
using FindThatBook.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Controllers;

/// <summary>Find That Book — book discovery endpoints.</summary>
[ApiController]
[Route("api/v1/books")]
[Produces("application/json")]
public class BookSearchController : ControllerBase
{
    private readonly SearchBooksUseCase _searchUseCase;
    private readonly IBookRepository _bookRepository;
    private readonly ILogger<BookSearchController> _logger;

    public BookSearchController(
        SearchBooksUseCase searchUseCase,
        IBookRepository bookRepository,
        ILogger<BookSearchController> logger)
    {
        _searchUseCase = searchUseCase;
        _bookRepository = bookRepository;
        _logger = logger;
    }

    /// <summary>
    /// Search for books using a messy plain-text query.
    /// The query may contain a title, author name (full or partial), character names, keywords, or any combination.
    /// </summary>
    /// <example>
    /// POST /api/v1/books/search
    /// { "query": "tolkien hobbit illustrated deluxe 1937", "maxResults": 5 }
    /// </example>
    [HttpPost("search")]
    [ProducesResponseType(typeof(BookSearchResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    [ProducesResponseType(typeof(ProblemDetails), 408)]
    [ProducesResponseType(typeof(ProblemDetails), 500)]
    public async Task<IActionResult> Search([FromBody] BookSearchRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _logger.LogInformation("Search request received: Query={Query}", request.Query);
        var result = await _searchUseCase.ExecuteAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                400 => BadRequest(new ProblemDetails { Status = 400, Title = "Bad Request", Detail = result.Error }),
                408 => StatusCode(408, new ProblemDetails { Status = 408, Title = "Timeout", Detail = result.Error }),
                503 => StatusCode(503, new ProblemDetails { Status = 503, Title = "Service Unavailable", Detail = result.Error }),
                _ => StatusCode(500, new ProblemDetails { Status = 500, Title = "Server Error", Detail = result.Error })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get full details for a specific Open Library work.
    /// </summary>
    /// <param name="workId">Open Library work ID, e.g. OL45804W</param>
    [HttpGet("{workId}")]
    [ProducesResponseType(typeof(BookDetailResponse), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> GetWork(string workId, CancellationToken cancellationToken)
    {
        var detail = await _bookRepository.GetWorkDetailsAsync(workId, cancellationToken);
        if (detail == null)
            return NotFound(new ProblemDetails { Status = 404, Title = "Not Found", Detail = $"Work '{workId}' not found." });
        return Ok(detail);
    }
}
