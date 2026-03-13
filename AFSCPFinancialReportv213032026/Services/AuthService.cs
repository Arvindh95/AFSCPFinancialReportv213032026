using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PX.Data;

namespace FinancialReport.Services
{
    public class AuthService : IDisposable
    {
        private string _accessToken;
        private string _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        private readonly string _baseUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _username;
        private readonly string _password;
        private readonly System.Threading.SemaphoreSlim _tokenLock = new System.Threading.SemaphoreSlim(1, 1);
        private bool _disposed;

        // Single HttpClient reused for all auth requests — avoids socket exhaustion
        private static readonly HttpClient _authClient = CreateAuthClient();

        private static HttpClient CreateAuthClient()
        {
            var handler = new HttpClientHandler { UseProxy = false };
            return new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(3) };
        }

        public AuthService(string baseUrl, string clientId, string clientSecret, string username, string password)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _clientId = clientId;
            _clientSecret = clientSecret;
            _username = username;
            _password = password;

            // Security: warn if credentials will be sent over plain HTTP
            if (_baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                PXTrace.WriteWarning("AuthService: base URL is HTTP — credentials and tokens are sent unencrypted. Use HTTPS in production.");
        }

        // Synchronous wrapper — runs on a thread-pool thread to avoid deadlock in sync contexts
        public string AuthenticateAndGetToken()
        {
            return Task.Run(() => AuthenticateAndGetTokenAsync()).GetAwaiter().GetResult();
        }

        // Async version
        public async Task<string> AuthenticateAndGetTokenAsync()
        {
            // Fast path — no lock needed, just read fields
            if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.Now)
                return _accessToken;

            // Only one thread/task fetches a new token at a time; others wait then hit the fast path
            await _tokenLock.WaitAsync();
            try
            {
                // Double-check: another task may have fetched while we waited
                if (!string.IsNullOrEmpty(_accessToken) && _tokenExpiry > DateTime.Now)
                    return _accessToken;

                // Try refresh first
                if (!string.IsNullOrEmpty(_refreshToken))
                {
                    try
                    {
                        return await RefreshAccessTokenAsync(_refreshToken);
                    }
                    catch (PXException ex)
                    {
                        PXTrace.WriteError($"Refresh failed: {ex.Message}. Falling back to password grant.");
                    }
                }

                // Password grant
                PXTrace.WriteInformation("Requesting a new access token...");
                string tokenUrl = $"{_baseUrl}/identity/connect/token";

                var form = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string,string>("grant_type",    "password"),
                    new KeyValuePair<string,string>("client_id",     _clientId),
                    new KeyValuePair<string,string>("client_secret", _clientSecret),
                    new KeyValuePair<string,string>("username",      _username),
                    new KeyValuePair<string,string>("password",      _password),
                    new KeyValuePair<string,string>("scope",         "api")
                });

                HttpResponseMessage resp = await _authClient.PostAsync(tokenUrl, form);

                if (!resp.IsSuccessStatusCode)
                {
                    PXTrace.WriteError($"Authentication failed with status {(int)resp.StatusCode} ({resp.StatusCode}).");
                    throw new PXException(Messages.FailedToAuthenticate);
                }

                string body = await resp.Content.ReadAsStringAsync();
                var json = JObject.Parse(body);
                _accessToken = json["access_token"]?.ToString()
                                ?? throw new PXException(Messages.AccessTokenNotFound);
                _refreshToken = json["refresh_token"]?.ToString() ?? string.Empty;

                int expiresIn = json["expires_in"]?.ToObject<int>() ?? 0;
                if (expiresIn == 0)
                    throw new PXException(Messages.TokenExpirationNotFound);

                // Buffer 60 seconds so token never expires mid-request
                _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

                PXTrace.WriteInformation("New access token retrieved.");
                return _accessToken;
            }
            finally
            {
                _tokenLock.Release();
            }
        }

        private async Task<string> RefreshAccessTokenAsync(string refreshToken)
        {
            string url = $"{_baseUrl}/identity/connect/token";

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string,string>("grant_type",    "refresh_token"),
                new KeyValuePair<string,string>("client_id",     _clientId),
                new KeyValuePair<string,string>("client_secret", _clientSecret),
                new KeyValuePair<string,string>("refresh_token", refreshToken)
            });

            HttpResponseMessage resp = await _authClient.PostAsync(url, form);

            if (!resp.IsSuccessStatusCode)
            {
                PXTrace.WriteError($"Token refresh failed with status {(int)resp.StatusCode} ({resp.StatusCode}).");
                throw new PXException(Messages.FailedToRefreshToken);
            }

            string body = await resp.Content.ReadAsStringAsync();
            var json = JObject.Parse(body);
            string newToken = json["access_token"]?.ToString()
                              ?? throw new PXException(Messages.AccessTokenNotFound);

            int expiresIn = json["expires_in"]?.ToObject<int>() ?? 0;
            if (expiresIn == 0)
                throw new PXException(Messages.TokenExpirationNotFound);

            // Caller holds _tokenLock — no inner lock needed
            _accessToken = newToken;
            _refreshToken = json["refresh_token"]?.ToString() ?? string.Empty;
            _tokenExpiry = DateTime.Now.AddSeconds(expiresIn - 60);

            PXTrace.WriteInformation("Access token refreshed.");
            return newToken;
        }

        /// <summary>True if an access token has been obtained and not yet cleared by Logout.</summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        // Synchronous wrapper — runs on a thread-pool thread to avoid deadlock in sync contexts
        public void Logout()
        {
            Task.Run(() => LogoutAsync()).GetAwaiter().GetResult();
        }

        // Async version
        public async Task LogoutAsync()
        {
            try
            {
                string url = $"{_baseUrl}/entity/auth/logout";
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                    HttpResponseMessage resp = await _authClient.SendAsync(request);
                    if (resp.IsSuccessStatusCode)
                        PXTrace.WriteInformation("Logged out successfully.");
                    else
                        PXTrace.WriteError($"Logout failed with status {(int)resp.StatusCode} ({resp.StatusCode}).");
                }
            }
            catch (Exception ex)
            {
                PXTrace.WriteError($"Logout exception: {ex.Message}");
            }
            finally
            {
                _accessToken = _refreshToken = null;
                _tokenExpiry = DateTime.MinValue;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _tokenLock.Dispose();
            _disposed = true;
        }
    }
}
