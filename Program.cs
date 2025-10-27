// using ExpertFinder.Api.Services; // <-- add this if your services are in a folder named Services
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ------------ CORS --------------
/*
 * Set the client origin here. Replace with the exact origin shown in your browser
 * (scheme + host + port), e.g. "https://localhost:7296".
 */
var clientOrigin = builder.Configuration["ClientOrigin"] ?? "https://localhost:7296";

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

// Register your custom services
builder.Services.AddHttpClient<IOpenAiService, OpenAiService>();
builder.Services.AddHttpClient<IGoogleSearchService, GoogleSearchService>();
builder.Services.AddLogging();
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

app.UseHttpsRedirection();

// IMPORTANT: Apply CORS BEFORE Authorization and MapControllers
app.UseCors("AllowClient");

app.UseAuthorization();

app.MapControllers();

app.Run();
