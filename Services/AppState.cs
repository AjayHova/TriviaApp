namespace TriviaApp.Services;

/// <summary>
/// Holds per-user session state for a Blazor Server circuit.
/// Registered as Scoped so each browser tab/connection gets its own instance.
/// </summary>
public class AppState
{
    public string? IdToken { get; set; }
    public string? Uid { get; set; }
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public bool IsLoggedIn => IdToken != null;

    public event Action? OnChange;

    public void Login(string idToken, string uid, string email, string? displayName)
    {
        IdToken = idToken;
        Uid = uid;
        Email = email;
        DisplayName = displayName;
        NotifyStateChanged();
    }

    public void Logout()
    {
        IdToken = null;
        Uid = null;
        Email = null;
        DisplayName = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
