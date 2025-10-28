// Program.cs
// using ExpertFinder.Api.Services; // <-- add this if your services are in a folder named Services
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- Ensure environment variables are available in IConfiguration (usually already true) ---
builder.Configuration.AddEnvironmentVariables();

// ------------ Read secrets / config (from appsettings.json OR environment variables) --------------
// Preference order: appsettings.json (if present) -> environment variables
var openAiKey = builder.Configuration["OpenAi:ApiKey"] ?? Environment.GetEnvironmentVariable("OpenAi__ApiKey");
var googleKey = builder.Configuration["Google:ApiKey"] ?? Environment.GetEnvironmentVariable("Google__ApiKey");
var googleCx = builder.Configuration["Google:SearchEngineId"] ?? Environment.GetEnvironmentVariable("Google__SearchEngineId");

// AUTH_TOKENS: comma-separated list expected, e.g. abcds,123456
var authTokensEnv = Environment.GetEnvironmentVariable("AUTH_TOKENS") ?? builder.Configuration["AUTH_TOKENS"] ?? string.Empty;
var validTokens = authTokensEnv
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Where(t => !string.IsNullOrWhiteSpace(t))
    .ToList();

// Optional: FRONTEND_URL for CORS whitelist (set this on Render / Vercel)
var clientOrigin = builder.Configuration["FRONTEND_URL"] ?? Environment.GetEnvironmentVariable("FRONTEND_URL");

//// Optional fail-fast or warning
//if (string.IsNullOrWhiteSpace(openAiKey) || string.IsNullOrWhiteSpace(googleKey) || string.IsNullOrWhiteSpace(googleCx))
//{
//    // Log a clear warning. In production you might want to throw to prevent startup.
//    builder.Logging.CreateLogger("Startup").LogWarning(
//        "One or more API keys are missing. Make sure OpenAi__ApiKey, Google__ApiKey, and Google__SearchEngineId are set in environment variables or appsettings.");
//}

// ---------- Register AppSecrets (typed holder) ----------
var appSecrets = new AppSecrets
{
    OpenAiKey = openAiKey ?? string.Empty,
    GoogleApiKey = googleKey ?? string.Empty,
    GoogleCx = googleCx ?? string.Empty,
    ValidTokens = validTokens
};
builder.Services.AddSingleton(appSecrets);

// ------------ CORS --------------
/*
 * Set the client origin here. Replace with the exact origin shown in your browser
 * (scheme + host + port), e.g. "https://your-frontend.vercel.app" or "https://localhost:7296".
 */
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClient", policy =>
    {
        policy.WithOrigins(clientOrigin)   // exact origin required for credentials
              .AllowAnyMethod()
              .AllowAnyHeader();
        // .AllowCredentials(); // uncomment only if you need cookies/credentials
    });
});
// ------------ end CORS ----------

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI configuration
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ExpertFinder API",
        Version = "v1",
        Description = "Backend API for the Expert Finder project. Handles authentication and API calls to ChatGPT and Google Search."
    });
});

// Register your custom services (HttpClients for external APIs)
builder.Services.AddHttpClient<IOpenAiService, OpenAiService>();
builder.Services.AddHttpClient<IGoogleSearchService, GoogleSearchService>();
builder.Services.AddLogging();

// Register AuthService - keep your existing registration
// If your AuthService accepts dependencies (like IConfiguration or AppSecrets) it will be resolved by DI.
// Make sure AuthService can read the valid tokens from AppSecrets or IConfiguration as implemented.
builder.Services.AddSingleton<ExpertFinder.Api.Services.AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ExpertFinder API v1");
        c.RoutePrefix = string.Empty; // Swagger at root (optional)
    });
}

// Important ordering: UseCors before UseAuthorization/MapControllers
app.UseHttpsRedirection();
app.UseCors("AllowClient");
app.UseAuthorization();

app.MapControllers();

// Run the app
app.Run();

/// <summary>
/// Typed secrets holder registered as singleton for DI.
/// You can inject AppSecrets wherever you need the OpenAI/Google keys or valid tokens.
/// </summary>
public class AppSecrets
{
    public string OpenAiKey { get; set; } = string.Empty;
    public string GoogleApiKey { get; set; } = string.Empty;
    public string GoogleCx { get; set; } = string.Empty;
    public List<string> ValidTokens { get; set; } = new();
}
