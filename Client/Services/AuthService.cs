using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace Client.Services
{
    public class AuthService : IAuthService
    {
        private const string TokenKey = "SafeVault.Token";
        private readonly HttpClient _http;
        private readonly IJSRuntime _js;

        public string? Token { get; private set; }
        public string? Username
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Token)) return null;
                try
                {
                    var parts = Token.Split('.');
                    if (parts.Length < 2) return null;
                    var payload = parts[1];
                    // pad base64
                    switch (payload.Length % 4)
                    {
                        case 2: payload += "=="; break;
                        case 3: payload += "="; break;
                    }
                    payload = payload.Replace('-', '+').Replace('_', '/');
                    var bytes = System.Convert.FromBase64String(payload);
                    var json = System.Text.Encoding.UTF8.GetString(bytes);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("name", out var name)) return name.GetString();
                    if (doc.RootElement.TryGetProperty("sub", out var sub)) return sub.GetString();
                }
                catch
                {
                    // ignore
                }
                return null;
            }
        }

        public event Action? AuthenticationStateChanged;

        public AuthService(HttpClient http, IJSRuntime js)
        {
            _http = http;
            _js = js;
        }

        public async Task InitializeAsync()
        {
            try
            {
                var token = await _js.InvokeAsync<string>("localStorage.getItem", TokenKey);
                if (!string.IsNullOrWhiteSpace(token))
                {
                    Token = token;
                    _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
                }
            }
            catch
            {
                // ignore JS interop issues in environments without localStorage
            }
        }

        public async Task<bool> AuthenticateAsync(string username, string password)
        {
            var payload = new { Username = username, Password = password };
            try
            {
                var resp = await _http.PostAsJsonAsync("authenticate", payload);
                if (!resp.IsSuccessStatusCode) return false;

                // API returns { "token": "..." }
                var doc = await resp.Content.ReadFromJsonAsync<TokenResponse>();
                if (doc?.token is null) return false;

                Token = doc.token;
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
                try { await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, Token); } catch { }
                AuthenticationStateChanged?.Invoke();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            Token = null;
            _http.DefaultRequestHeaders.Authorization = null;
            try { await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey); } catch { }
            AuthenticationStateChanged?.Invoke();
        }

        private class TokenResponse { public string? token { get; set; } }
    }
}
