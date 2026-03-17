using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Middleware;

/// <summary>
/// Global exception middleware that catches unhandled exceptions and returns
/// consistent RFC 7807 ProblemDetails responses with a correlation ID.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogError(ex, "Unhandled exception [{CorrelationId}]: {Message}", correlationId, ex.Message);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = 500,
                Title = "An unexpected error occurred.",
                Detail = "Please try again. If the problem persists, contact support.",
                Extensions = { ["correlationId"] = correlationId }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
    }
}
