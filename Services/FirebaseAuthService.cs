using System.Net.Http.Json;
using System.Text.Json;

namespace TriviaApp.Services;

public class FirebaseAuthService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly AppState _state;

    public FirebaseAuthService(IConfiguration config, AppState state)
    {
        _apiKey = config["Firebase:ApiKey"]!;
        _http = new HttpClient();
        _state = state;
    }

    public async Task<string?> RegisterAsync(string email, string password, string username)
    {
        try
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_apiKey}";
            var res = await _http.PostAsJsonAsync(url, new
            {
                email,
                password,
                displayName = username,
                returnSecureToken = true
            });

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();

            if (json.TryGetProperty("error", out var err))
                return err.GetProperty("message").GetString();

            var idToken = json.GetProperty("idToken").GetString()!;
            var uid = json.GetProperty("localId").GetString()!;
            _state.Login(idToken, uid, email, username);
            return null;
        }
        catch (Exception ex)
        {
            return $"Network error: {ex.Message}";
        }
    }

    public async Task<string?> LoginAsync(string email, string password)
    {
        try
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";
            var res = await _http.PostAsJsonAsync(url, new
            {
                email,
                password,
                returnSecureToken = true
            });

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();

            if (json.TryGetProperty("error", out var err))
                return err.GetProperty("message").GetString();

            var idToken = json.GetProperty("idToken").GetString()!;
            var uid = json.GetProperty("localId").GetString()!;

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

            _state.Login(idToken, uid, email, displayName);
            return null;
        }
        catch (Exception ex)
        {
            return $"Network error: {ex.Message}";
        }
    }

    public void Logout() => _state.Logout();
}
