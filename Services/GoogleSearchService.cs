using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ExpertFinder.Api.Services
{
    public class GoogleSearchService : IGoogleSearchService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _cx;
        private readonly ILogger<GoogleSearchService> _logger;

        public GoogleSearchService(HttpClient http, AppSecrets secrets, ILogger<GoogleSearchService> logger)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (secrets == null) throw new ArgumentNullException(nameof(secrets));

            _apiKey = secrets.GoogleApiKey ?? string.Empty;
            _cx = secrets.GoogleCx ?? string.Empty;

            if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_cx))
            {
                // Fail fast so deployment logs show a clear problem
                var msg = "Google API key or Search Engine ID (cx) is missing. " +
                          "Set Google__ApiKey and Google__SearchEngineId (env) or populate AppSecrets.";
                _logger.LogError(msg);
                throw new InvalidOperationException(msg);
            }
        }

        public async Task<List<ExpertLink>> SearchAsync(string topic, string location, int maxResults = 8)
        {
            // Force maxResults <= 10 (Google API limit)
            var num = Math.Min(maxResults, 10);

            // Build a safe query: site:linkedin.com/in "topic" location
            var safeTopic = (topic ?? string.Empty).Trim();
            var safeLocation = (location ?? string.Empty).Trim();

            // If topic is empty, return empty list (or you may choose to throw)
            if (string.IsNullOrEmpty(safeTopic))
            {
                _logger.LogInformation("SearchAsync called with empty topic; returning no results.");
                return new List<ExpertLink>();
            }

            var q = $"site:linkedin.com/in \"{safeTopic}\" {safeLocation}".Trim();
            var encodedQ = Uri.EscapeDataString(q);

            var url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_cx}&q={encodedQ}&num={num}";

            // Log that a search is being performed but do NOT log the API key or the full URL with key
            _logger.LogInformation("Performing Google Custom Search for topic='{Topic}' location='{Location}' (results={Num})",
                safeTopic, safeLocation, num);

            using var res = await _http.GetAsync(url);
            var raw = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                // log status and response body for debugging (body may contain error details); do not leak keys
                _logger.LogError("Google Custom Search returned {StatusCode}. Body: {Body}", (int)res.StatusCode, raw);
                throw new HttpRequestException($"Google Custom Search returned {(int)res.StatusCode}. See logs for details.");
            }

            using var doc = JsonDocument.Parse(raw);
            var list = new List<ExpertLink>();

            if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var it in items.EnumerateArray())
                {
                    var title = it.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                    var link = it.TryGetProperty("link", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() ?? "" : "";
                    var snippet = it.TryGetProperty("snippet", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() ?? "" : "";

                    if (!string.IsNullOrWhiteSpace(link))
                    {
                        list.Add(new ExpertLink
                        {
                            Title = title,
                            Url = link,
                            Snippet = snippet
                        });
                    }
                }
            }

            // Prefer LinkedIn results first (the query already targets LinkedIn but keep ordering safe)
            var ordered = list
                .OrderByDescending(l => l.Url != null && l.Url.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase))
                .Take(num)
                .ToList();

            _logger.LogInformation("Google Search returned {Count} items (after ordering/take).", ordered.Count);

            return ordered;
        }
    }

    //public class ExpertLink
    //{
    //    public string Title { get; set; } = string.Empty;
    //    public string Url { get; set; } = string.Empty;
    //    public string Snippet { get; set; } = string.Empty;
    //}
}
