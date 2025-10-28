using System.Collections.Concurrent;

namespace ExpertFinder.Api.Services
{
    public class AuthService
    {
        private readonly ConcurrentDictionary<string, byte> _oneTimeTokens = new();
        private readonly ConcurrentDictionary<string, DateTime> _sessions = new();
        private readonly TimeSpan _sessionLifetime = TimeSpan.FromHours(4);

        public AuthService()
        {
            // Load tokens directly from Render environment variable
            var envTokens = Environment.GetEnvironmentVariable("AUTH_TOKENS");

            if (!string.IsNullOrEmpty(envTokens))
            {
                var tokens = envTokens.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (var t in tokens)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                        _oneTimeTokens.TryAdd(t.Trim(), 0);
                }
            }
        }

        public string ValidateAndConsumeOneTimeToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            token = token.Trim();

            if (_oneTimeTokens.TryRemove(token, out _))
            {
                var session = Guid.NewGuid().ToString("N");
                var expiry = DateTime.UtcNow.Add(_sessionLifetime);
                _sessions[session] = expiry;
                return session;
            }

            return null;
        }

        public bool ValidateSessionToken(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken)) return false;
            if (!_sessions.TryGetValue(sessionToken, out var expiry)) return false;

            if (expiry < DateTime.UtcNow)
            {
                _sessions.TryRemove(sessionToken, out _);
                return false;
            }

            return true;
        }

        public void RevokeSessionToken(string sessionToken)
        {
            if (!string.IsNullOrWhiteSpace(sessionToken))
                _sessions.TryRemove(sessionToken, out _);
        }

        public bool IsOneTimeTokenUnused(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            return _oneTimeTokens.ContainsKey(token.Trim());
        }
    }
}
