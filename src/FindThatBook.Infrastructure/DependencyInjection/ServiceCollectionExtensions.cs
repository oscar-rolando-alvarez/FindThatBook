using FindThatBook.Application.Interfaces;
using FindThatBook.Domain.Services;
using FindThatBook.Infrastructure.AI;
using FindThatBook.Infrastructure.OpenLibrary;
using FindThatBook.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FindThatBook.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Options
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.Section));
        services.Configure<OpenLibraryOptions>(configuration.GetSection(OpenLibraryOptions.Section));

        // Domain services
        services.AddSingleton<BookMatchingService>();

        // AI extractors (Gemini wraps RegexFallback as its fallback)
        services.AddSingleton<RegexFallbackExtractor>();
        services.AddHttpClient<GeminiExtractor>();
        services.AddScoped<IAiExtractor, GeminiExtractor>();

        // Open Library HTTP client with timeout
        // Register via interface so DI resolves IBookRepository with the configured typed HttpClient
        var olOptions = configuration.GetSection(OpenLibraryOptions.Section).Get<OpenLibraryOptions>()
                        ?? new OpenLibraryOptions();
        services.AddHttpClient<IBookRepository, OpenLibraryClient>(client =>
        {
            client.BaseAddress = new Uri(olOptions.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(olOptions.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("User-Agent", "FindThatBook/1.0 (github.com/oscaralvarez/find-that-book)");
        });

        return services;
    }
}
