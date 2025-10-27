using System.Net.Http;
using System.Text.Json;

public class GoogleSearchService : IGoogleSearchService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _cx;
    private readonly ILogger<GoogleSearchService> _logger;

    public GoogleSearchService(HttpClient http, IConfiguration config, ILogger<GoogleSearchService> logger)
    {
        _http = http;
        _apiKey = config["Google:ApiKey"] ?? "";
        _cx = config["Google:Cx"] ?? "";
        _logger = logger;
    }

    public async Task<List<ExpertLink>> SearchAsync(string topic, string location, int maxResults = 8)
    {
        // Force maxResults <= 10 (Google API limit)
        var num = Math.Min(maxResults, 10);

        // Build a query like: site:linkedin.com/in "computer vision" Stuttgart
        var q = $"site:linkedin.com/in \"{topic}\" {location}";
        var encodedQ = Uri.EscapeDataString(q);

        var url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_cx}&q={encodedQ}&num={num}";

        _logger.LogInformation("Google Search URL: {Url}", url);

        var res = await _http.GetAsync(url);

        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            // log full response body for debugging
            _logger.LogError("Google Custom Search returned {StatusCode}. Body: {Body}", (int)res.StatusCode, raw);
            throw new HttpRequestException($"Google Custom Search returned {(int)res.StatusCode}. Response body: {raw}");
        }

        using var doc = JsonDocument.Parse(raw);
        var list = new List<ExpertLink>();

        if (doc.RootElement.TryGetProperty("items", out var items))
        {
            foreach (var it in items.EnumerateArray())
            {
                var link = new ExpertLink
                {
                    Title = it.GetProperty("title").GetString() ?? "",
                    Url = it.GetProperty("link").GetString() ?? "",
                    Snippet = it.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : ""
                };
                list.Add(link);
            }
        }

        // prefer linkedin links first (should already be linkedin due to site:), keep order
        var ordered = list.OrderByDescending(l => l.Url.Contains("linkedin.com")).Take(num).ToList();
        return ordered;
    }
}
