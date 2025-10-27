using Microsoft.AspNetCore.Mvc;
using Markdig;
using Ganss.Xss;
using ExpertFinder.Api.Services; // for AuthService

namespace ExpertFinder.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly IOpenAiService _openAi;
        private readonly IGoogleSearchService _google;
        private readonly AuthService _auth;

        public SearchController(IOpenAiService openAi, IGoogleSearchService google, AuthService auth)
        {
            _openAi = openAi;
            _google = google;
            _auth = auth;
        }

        [HttpPost]
        public async Task<IActionResult> Search([FromBody] SearchRequest req)
        {
            // --- Validate session token from Authorization header (Bearer <sessionToken>) ---
            var authHeader = Request.Headers["Authorization"].ToString();
            string sessionToken = null;
            if (!string.IsNullOrWhiteSpace(authHeader))
            {
                if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    sessionToken = authHeader.Substring("Bearer ".Length).Trim();
                else
                    sessionToken = authHeader.Trim();
            }

            if (!_auth.ValidateSessionToken(sessionToken))
                return Unauthorized();

            // --- Validate request body ---
            if (req == null || string.IsNullOrWhiteSpace(req.Query) || string.IsNullOrWhiteSpace(req.Location))
                return BadRequest("Query and Location required");

            // 1. Get ChatGPT answer (raw markdown/plain text)
            var rawAnswer = await _openAi.GetAnswerAsync(req.Query) ?? string.Empty;

            // 2. Extract topic (trim and fallback to query if needed)
            var topic = (await _openAi.ExtractTopicAsync(req.Query))?.Trim();
            if (string.IsNullOrWhiteSpace(topic))
            {
                // fallback: use a shortened version of the query as topic
                topic = req.Query.Length <= 100 ? req.Query : req.Query.Substring(0, 100);
            }

            // 3. Query Google Custom Search with topic + location
            var experts = await _google.SearchAsync(topic, req.Location);

            // 4. Convert Markdown -> HTML (Markdig) and sanitize (Ganss.XSS)
            string cleanedHtml;
            try
            {
                var pipeline = new MarkdownPipelineBuilder()
                    .UseAdvancedExtensions() // tables, fenced code blocks, autolinks, etc.
                    .Build();

                var html = Markdig.Markdown.ToHtml(rawAnswer, pipeline);

                var sanitizer = new HtmlSanitizer();

                // allow formatting and code-related tags commonly used in Markdown
                sanitizer.AllowedTags.Add("pre");
                sanitizer.AllowedTags.Add("code");
                sanitizer.AllowedTags.Add("table");
                sanitizer.AllowedTags.Add("thead");
                sanitizer.AllowedTags.Add("tbody");
                sanitizer.AllowedTags.Add("tr");
                sanitizer.AllowedTags.Add("th");
                sanitizer.AllowedTags.Add("td");
                sanitizer.AllowedTags.Add("blockquote");
                sanitizer.AllowedTags.Add("hr");
                sanitizer.AllowedTags.Add("img");      // optional: allow images
                sanitizer.AllowedAttributes.Add("src"); // for images
                sanitizer.AllowedAttributes.Add("alt");
                sanitizer.AllowedAttributes.Add("title");
                sanitizer.AllowedAttributes.Add("class"); // allow classes for code blocks (syntax highlighting)
                sanitizer.AllowedAttributes.Add("href");  // allow links
                sanitizer.AllowedAttributes.Add("target"); // allow target="_blank"
                sanitizer.AllowDataAttributes = false;

                cleanedHtml = sanitizer.Sanitize(html);
            }
            catch
            {
                // If something goes wrong in conversion/sanitization, fallback to raw text escaped as HTML
                var fallback = System.Net.WebUtility.HtmlEncode(rawAnswer ?? string.Empty);
                cleanedHtml = $"<pre>{fallback}</pre>";
            }

            var resp = new SearchResponse
            {
                Answer = cleanedHtml,
                Topic = topic,
                Experts = experts
            };

            return Ok(resp);
        }
    }
}
