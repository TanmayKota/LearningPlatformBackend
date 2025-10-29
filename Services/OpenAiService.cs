using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ExpertFinder.Api.Services
{
    public class OpenAiService : IOpenAiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public OpenAiService(HttpClient http, AppSecrets secrets)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            if (secrets == null) throw new ArgumentNullException(nameof(secrets));

            _apiKey = secrets.OpenAiKey;
            if (string.IsNullOrWhiteSpace(_apiKey))
                throw new ArgumentException("OpenAI API key is missing. Set OpenAi__ApiKey (env) or configure AppSecrets.", nameof(secrets));
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
                model = "gpt-4o-mini", // change model if needed
                messages = new[] {
                    new { role = "system", content = "You are an expert assistant." },
                    new { role = "user", content = $"Answer the following query:\n\n{userQuery}" }
                },
                max_tokens = 800
            };

            var req = CreateRequest(body);
            using var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            using var s = await res.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);
            // defensive parsing
            if (doc.RootElement.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var contentEl)
                    && contentEl.ValueKind == JsonValueKind.String)
                {
                    return contentEl.GetString() ?? string.Empty;
                }
            }

            // fallback: try to return whole response as string if structure unexpected
            return doc.RootElement.ToString();
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
            using var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            using var s = await res.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);

            if (doc.RootElement.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var message)
                    && message.TryGetProperty("content", out var contentEl)
                    && contentEl.ValueKind == JsonValueKind.String)
                {
                    return contentEl.GetString()?.Trim() ?? string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
