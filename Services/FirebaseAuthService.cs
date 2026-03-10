using System.Net.Http.Json;
using System.Text.Json;

namespace TriviaApp.Services;

public class FirebaseAuthService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly AppState _state;
    private readonly CookieAuthService _cookies;

    public FirebaseAuthService(IConfiguration config, AppState state, CookieAuthService cookies)
    {
        _apiKey  = config["Firebase:ApiKey"]!;
        _http    = new HttpClient();
        _state   = state;
        _cookies = cookies;
    }

    // ── Register ─────────────────────────────────────────────────────────────

    public async Task<string?> RegisterAsync(string email, string password, string username)
    {
        try
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_apiKey}";
            var res = await _http.PostAsJsonAsync(url, new
            {
                email, password, displayName = username, returnSecureToken = true
            });

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("error", out var err))
                return err.GetProperty("message").GetString();

            var idToken      = json.GetProperty("idToken").GetString()!;
            var uid          = json.GetProperty("localId").GetString()!;
            var refreshToken = json.GetProperty("refreshToken").GetString()!;

            _cookies.SetRefreshToken(refreshToken);
            _state.Login(idToken, uid, email, username);
            return null;
        }
        catch (Exception ex) { return $"Network error: {ex.Message}"; }
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    public async Task<string?> LoginAsync(string email, string password)
    {
        try
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";
            var res = await _http.PostAsJsonAsync(url, new
            {
                email, password, returnSecureToken = true
            });

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("error", out var err))
                return err.GetProperty("message").GetString();

            var idToken      = json.GetProperty("idToken").GetString()!;
            var uid          = json.GetProperty("localId").GetString()!;
            var refreshToken = json.GetProperty("refreshToken").GetString()!;

            // Fetch display name
            string? displayName = null;
            try
            {
                var profileUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={_apiKey}";
                var profileRes = await _http.PostAsJsonAsync(profileUrl, new { idToken });
                var profileJson = await profileRes.Content.ReadFromJsonAsync<JsonElement>();
                if (profileJson.TryGetProperty("users", out var users) && users.GetArrayLength() > 0)
                {
                    var u = users[0];
                    if (u.TryGetProperty("displayName", out var dn))
                        displayName = dn.GetString();
                }
            }
            catch { /* non-critical */ }

            _cookies.SetRefreshToken(refreshToken);
            _state.Login(idToken, uid, email, displayName);
            return null;
        }
        catch (Exception ex) { return $"Network error: {ex.Message}"; }
    }

    // ── Restore session from cookie ───────────────────────────────────────────

    /// <summary>
    /// Called on app start. If a refresh token cookie exists, exchanges it
    /// for a fresh ID token and restores the session silently.
    /// </summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        var refreshToken = _cookies.GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken)) return false;

        try
        {
            // Exchange refresh token for a new ID token via Firebase Token REST API
            var url = $"https://securetoken.googleapis.com/v1/token?key={_apiKey}";
            var res = await _http.PostAsJsonAsync(url, new
            {
                grant_type    = "refresh_token",
                refresh_token = refreshToken
            });

            if (!res.IsSuccessStatusCode) { _cookies.ClearRefreshToken(); return false; }

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();

            if (!json.TryGetProperty("id_token", out var idTokenEl))
            { _cookies.ClearRefreshToken(); return false; }

            var idToken         = idTokenEl.GetString()!;
            var uid             = json.GetProperty("user_id").GetString()!;
            var newRefreshToken = json.GetProperty("refresh_token").GetString()!;

            // Update cookie with rotated refresh token
            _cookies.SetRefreshToken(newRefreshToken);

            // Fetch email + display name
            string? email = null, displayName = null;
            try
            {
                var profileUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={_apiKey}";
                var profileRes = await _http.PostAsJsonAsync(profileUrl, new { idToken });
                var profileJson = await profileRes.Content.ReadFromJsonAsync<JsonElement>();
                if (profileJson.TryGetProperty("users", out var users) && users.GetArrayLength() > 0)
                {
                    var u = users[0];
                    if (u.TryGetProperty("email", out var em))       email       = em.GetString();
                    if (u.TryGetProperty("displayName", out var dn)) displayName = dn.GetString();
                }
            }
            catch { /* non-critical */ }

            _state.Login(idToken, uid, email ?? "", displayName);
            return true;
        }
        catch
        {
            _cookies.ClearRefreshToken();
            return false;
        }
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    public void Logout()
    {
        _cookies.ClearRefreshToken();
        _state.Logout();
    }
}
