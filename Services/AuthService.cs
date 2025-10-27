using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace ExpertFinder.Api.Services
{
    public class AuthService
    {
        // one-time access tokens (loaded from configuration)
        private readonly ConcurrentDictionary<string, byte> _oneTimeTokens = new();

        // issued session tokens -> expiry
        private readonly ConcurrentDictionary<string, DateTime> _sessions = new();

        // session lifetime (adjust as needed)
        private readonly TimeSpan _sessionLifetime = TimeSpan.FromHours(4);

        public AuthService(IConfiguration config)
        {
            // load configured one-time tokens from configuration (Auth:ValidTokens)
            var tokens = config.GetSection("Auth:ValidTokens").Get<string[]>() ?? Array.Empty<string>();
            foreach (var t in tokens)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    _oneTimeTokens.TryAdd(t.Trim(), 0);
            }
        }

        /// <summary>
        /// Validate and consume a one-time access token. If valid and unused, consumes it and returns a new session token.
        /// If invalid or used, returns null.
        /// </summary>
        public string ValidateAndConsumeOneTimeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            token = token.Trim();

            // Try to remove token atomically. If removed, we can issue a session token.
            if (_oneTimeTokens.TryRemove(token, out _))
            {
                var session = Guid.NewGuid().ToString("N");
                var expiry = DateTime.UtcNow.Add(_sessionLifetime);
                _sessions[session] = expiry;
                return session;
            }

            // token not present (invalid or already used)
            return null;
        }

        /// <summary>
        /// Validate a session token (used for subsequent API calls).
        /// </summary>
        public bool ValidateSessionToken(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) return false;
            if (!_sessions.TryGetValue(sessionToken, out var expiry)) return false;

            if (expiry < DateTime.UtcNow)
            {
                // expired -> remove
                _sessions.TryRemove(sessionToken, out _);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Optional: revoke a session token.
        /// </summary>
        public void RevokeSessionToken(string sessionToken)
        {
            if (!string.IsNullOrWhiteSpace(sessionToken))
                _sessions.TryRemove(sessionToken, out _);
        }

        /// <summary>
        /// Optional helper to check whether a one-time token still exists (unused).
        /// </summary>
        public bool IsOneTimeTokenUnused(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            return _oneTimeTokens.ContainsKey(token.Trim());
        }
    }
}
