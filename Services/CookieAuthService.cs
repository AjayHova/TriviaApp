using Microsoft.AspNetCore.Http;

namespace TriviaApp.Services;

/// <summary>
/// Reads and writes the auth refresh token cookie server-side.
/// HTTP-only so JavaScript cannot access it.
/// </summary>
public class CookieAuthService
{
    private const string CookieName = "trivia_refresh";
    private readonly IHttpContextAccessor _http;

    public CookieAuthService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string? GetRefreshToken()
    {
        _http.HttpContext?.Request.Cookies.TryGetValue(CookieName, out var token);
        return token;
    }

    public void SetRefreshToken(string refreshToken)
    {
        _http.HttpContext?.Response.Cookies.Append(CookieName, refreshToken, new CookieOptions
        {
            HttpOnly  = true,                          // not accessible via JS
            Secure    = false,                         // set true in production with HTTPS
            SameSite  = SameSiteMode.Strict,
            Expires   = DateTimeOffset.UtcNow.AddDays(30),
            Path      = "/"
        });
    }

    public void ClearRefreshToken()
    {
        _http.HttpContext?.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
    }
}
