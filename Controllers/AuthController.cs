using Microsoft.AspNetCore.Mvc;
using ExpertFinder.Api.Services;

namespace ExpertFinder.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _auth;

        public AuthController(AuthService auth)
        {
            _auth = auth;
        }

        [HttpPost("validate")]
        public IActionResult Validate([FromBody] Dictionary<string, string> body)
        {
            if (body == null || !body.TryGetValue("token", out var token) || string.IsNullOrWhiteSpace(token))
                return BadRequest(new { message = "Please enter a Token" });

            // Consume the one-time token and get a new session token
            var sessionToken = _auth.ValidateAndConsumeOneTimeToken(token);

            if (sessionToken != null)
            {
                // Return session token to client. Client should use this session token for subsequent API calls.
                return Ok(new { valid = true, sessionToken });
            }

            // token invalid or already used
            return Unauthorized(new { valid = false, message = "Invalid Token - Please add a Valid Token" });
        }
    }
}
