using FindThatBook.Api.Middleware;
using FindThatBook.Application.UseCases;
using FindThatBook.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ── Services ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Find That Book API",
        Version = "v1",
        Description = "AI-powered library discovery: submit a messy query, get ranked book candidates from Open Library."
    });
    // Include XML comments for richer Swagger docs
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// Infrastructure (Gemini AI + Open Library + Domain services)
builder.Services.AddInfrastructure(builder.Configuration);

// Application use cases
builder.Services.AddScoped<SearchBooksUseCase>();

// CORS — allow React dev server and any deployed frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// ── Pipeline ────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Find That Book API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.MapControllers();

app.Run();

// Needed for WebApplicationFactory in integration tests
public partial class Program { }
