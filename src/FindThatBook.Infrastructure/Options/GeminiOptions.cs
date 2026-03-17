namespace FindThatBook.Infrastructure.Options;

public class GeminiOptions
{
    public const string Section = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash";
    public int TimeoutSeconds { get; set; } = 8;
}
