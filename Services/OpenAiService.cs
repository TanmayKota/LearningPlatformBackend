using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

public class OpenAiService : IOpenAiService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    public OpenAiService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["OpenAi:ApiKey"];
    }

    private HttpRequestMessage CreateRequest(object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        return req;
    }

    public async Task<string> GetAnswerAsync(string userQuery)
    {
        var body = new
        {
            model = "gpt-4o-mini", // or gpt-4o, gpt-4, use your model
            messages = new[] {
                new { role = "system", content = "You are an expert assistant." },
                new { role = "user", content = $"Answer the following query:\n\n{userQuery}" }
            },
            max_tokens = 800
        };
        var req = CreateRequest(body);
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        using var s = await res.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(s);
        // parse choices[0].message.content
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return content;
    }

    public async Task<string> ExtractTopicAsync(string userQuery)
    {
        var body = new
        {
            model = "gpt-4o-mini",
            messages = new[] {
                new { role = "system", content = "You are a topic extractor. Output 1-3 words, the main topic only." },
                new { role = "user", content = $"Extract the single main topic (1-3 words) from:\n\n{userQuery}" }
            },
            max_tokens = 10,
            temperature = 0
        };
        var req = CreateRequest(body);
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        using var s = await res.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(s);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        return content?.Trim();
    }
}
