using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace LibretaCobros.Services;

/// <summary>
/// Sesión de Supabase Auth (GoTrue) vía REST. Guarda access/refresh token en localStorage
/// y renueva el access token solo antes de que caduque.
/// </summary>
public class SupabaseAuthService
{
    private const string StorageKey = "lc_session";
    private static readonly string AuthUrl = $"{AppInfo.SupabaseUrl}/auth/v1";

    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private AuthSession? _session;
    private bool _loaded;

    public event Action? OnSessionChanged;

    public SupabaseAuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public bool IsSignedIn => _session != null;
    public string? UserId => _session?.UserId;
    public string? Email => _session?.Email;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var raw = await _js.InvokeAsync<string?>("blazorLocalStorage.get", StorageKey);
            if (!string.IsNullOrEmpty(raw))
            {
                _session = JsonSerializer.Deserialize<AuthSession>(raw);
            }
        }
        catch
        {
            _session = null;
        }
    }

    /// <summary>Devuelve un access token válido (renovándolo si hace falta) o null si no hay sesión.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        await LoadAsync();
        if (_session == null) return null;

        if (_session.ExpiresAtUtc <= DateTime.UtcNow.AddSeconds(60))
        {
            var ok = await RefreshAsync();
            if (!ok) return null;
        }
        return _session?.AccessToken;
    }

    public async Task<(bool Success, string Error)> SignInWithPasswordAsync(string email, string password)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{AuthUrl}/token?grant_type=password");
            req.Headers.Add("apikey", AppInfo.SupabaseAnonKey);
            req.Content = JsonContent.Create(new { email, password });
            using var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return (false, TranslateError(body, "No se pudo iniciar sesión."));
            }
            await StoreSessionAsync(ParseSession(body));
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"Sin conexión con el servidor: {ex.Message}");
        }
    }

    public async Task<(bool Success, bool HasSession, string Error)> SignUpAsync(string email, string password)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{AuthUrl}/signup");
            req.Headers.Add("apikey", AppInfo.SupabaseAnonKey);
            req.Content = JsonContent.Create(new { email, password });
            using var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return (false, false, TranslateError(body, "No se pudo crear la cuenta."));
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("access_token", out _))
            {
                await StoreSessionAsync(ParseSession(body));
                return (true, true, "");
            }
            return (true, false, "");
        }
        catch (Exception ex)
        {
            return (false, false, $"Sin conexión con el servidor: {ex.Message}");
        }
    }

    public async Task<bool> RefreshAsync()
    {
        if (_session == null) return false;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{AuthUrl}/token?grant_type=refresh_token");
            req.Headers.Add("apikey", AppInfo.SupabaseAnonKey);
            req.Content = JsonContent.Create(new { refresh_token = _session.RefreshToken });
            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                if ((int)resp.StatusCode is 400 or 401 or 403)
                {
                    await ClearAsync();
                }
                return false;
            }
            var body = await resp.Content.ReadAsStringAsync();
            await StoreSessionAsync(ParseSession(body));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, string Error)> UpdatePasswordAsync(string newPassword)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return (false, "No hay sesión activa.");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Put, $"{AuthUrl}/user");
            req.Headers.Add("apikey", AppInfo.SupabaseAnonKey);
            req.Headers.Add("Authorization", $"Bearer {token}");
            req.Content = JsonContent.Create(new { password = newPassword });
            using var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return (false, TranslateError(body, "No se pudo cambiar la contraseña."));
            }
            return (true, "");
        }
        catch (Exception ex)
        {
            return (false, $"Sin conexión con el servidor: {ex.Message}");
        }
    }

    public async Task SignOutAsync()
    {
        var token = _session?.AccessToken;
        await ClearAsync();
        if (token == null) return;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{AuthUrl}/logout");
            req.Headers.Add("apikey", AppInfo.SupabaseAnonKey);
            req.Headers.Add("Authorization", $"Bearer {token}");
            using var _ = await _http.SendAsync(req);
        }
        catch
        {
            // La sesión local ya está borrada; el token caduca solo.
        }
    }

    private async Task StoreSessionAsync(AuthSession session)
    {
        _session = session;
        await _js.InvokeVoidAsync("blazorLocalStorage.set", StorageKey, JsonSerializer.Serialize(session));
        OnSessionChanged?.Invoke();
    }

    private async Task ClearAsync()
    {
        _session = null;
        try { await _js.InvokeVoidAsync("blazorLocalStorage.remove", StorageKey); } catch { }
        OnSessionChanged?.Invoke();
    }

    private static AuthSession ParseSession(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
        var user = root.GetProperty("user");
        return new AuthSession
        {
            AccessToken = root.GetProperty("access_token").GetString() ?? "",
            RefreshToken = root.GetProperty("refresh_token").GetString() ?? "",
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn),
            UserId = user.GetProperty("id").GetString() ?? "",
            Email = user.TryGetProperty("email", out var em) ? em.GetString() ?? "" : ""
        };
    }

    private static string TranslateError(string body, string fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            string? msg = null;
            if (root.TryGetProperty("error_description", out var d)) msg = d.GetString();
            else if (root.TryGetProperty("msg", out var m)) msg = m.GetString();
            else if (root.TryGetProperty("message", out var m2)) msg = m2.GetString();
            else if (root.TryGetProperty("error_code", out var c)) msg = c.GetString();

            return msg switch
            {
                null => fallback,
                "Invalid login credentials" => "Email o contraseña incorrectos.",
                "Email not confirmed" => "Tu email todavía no está confirmado. Revisá tu correo.",
                "User already registered" => "Ya existe una cuenta con ese email.",
                "Signup requires a valid password" => "La contraseña no es válida.",
                _ when msg.Contains("Password should be at least") => "La contraseña es demasiado corta.",
                _ when msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) => "Demasiados intentos. Esperá un momento.",
                _ => msg
            };
        }
        catch
        {
            return fallback;
        }
    }

    private class AuthSession
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
        [JsonPropertyName("expires_at")] public DateTime ExpiresAtUtc { get; set; }
        [JsonPropertyName("user_id")] public string UserId { get; set; } = "";
        [JsonPropertyName("email")] public string Email { get; set; } = "";
    }
}
