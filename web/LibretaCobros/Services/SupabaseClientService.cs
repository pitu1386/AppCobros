using System.Net;
using System.Text;
using System.Text.Json;

namespace LibretaCobros.Services;

/// <summary>Error de una llamada a Supabase (PostgREST o RPC). El mensaje ya está listo para mostrar al usuario.</summary>
public class SupabaseException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public SupabaseException(string message, HttpStatusCode? status = null, Exception? inner = null) : base(message, inner)
    {
        StatusCode = status;
    }
}

/// <summary>
/// Cliente PostgREST de Supabase. Cada petición lleva la clave pública y, si hay sesión,
/// el access token del usuario para que apliquen las políticas RLS.
/// Las operaciones lanzan <see cref="SupabaseException"/> si el servidor rechaza la petición.
/// </summary>
public class SupabaseClientService
{
    private static readonly string RestUrl = $"{AppInfo.SupabaseUrl}/rest/v1";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
    };

    private readonly HttpClient _http;
    private readonly SupabaseAuthService _auth;

    public SupabaseClientService(HttpClient http, SupabaseAuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("apikey", AppInfo.SupabaseAnonKey);
        var token = await _auth.GetAccessTokenAsync();
        req.Headers.Add("Authorization", $"Bearer {token ?? AppInfo.SupabaseAnonKey}");
        return req;
    }

    private async Task<string> SendAsync(HttpMethod method, string url, object? body = null, string? prefer = null)
    {
        HttpResponseMessage resp;
        try
        {
            using var req = await CreateRequestAsync(method, url);
            if (prefer != null) req.Headers.Add("Prefer", prefer);
            if (body != null)
            {
                req.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
            }
            resp = await _http.SendAsync(req);
        }
        catch (Exception ex)
        {
            throw new SupabaseException("Sin conexión con la nube. Revisá tu internet e intentá de nuevo.", null, ex);
        }

        using (resp)
        {
            var text = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode) return text;
            throw new SupabaseException(TranslateError(resp.StatusCode, text), resp.StatusCode);
        }
    }

    public async Task<List<T>> GetAsync<T>(string table, string query = "select=*")
    {
        var json = await SendAsync(HttpMethod.Get, $"{RestUrl}/{table}?{query}");
        return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
    }

    public async Task UpsertAsync<T>(string table, IEnumerable<T> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0) return;
        await SendAsync(HttpMethod.Post, $"{RestUrl}/{table}", list, "resolution=merge-duplicates,return=minimal");
    }

    public Task UpsertRowAsync<T>(string table, T row) => UpsertAsync(table, new List<T> { row });

    /// <summary>Borra filas. El filtro es sintaxis PostgREST, p. ej. "id=eq.abc" o "cliente_id=eq.abc".</summary>
    public async Task DeleteAsync(string table, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) throw new ArgumentException("Un DELETE sin filtro borraría toda la tabla.", nameof(filter));
        await SendAsync(HttpMethod.Delete, $"{RestUrl}/{table}?{filter}", null, "return=minimal");
    }

    public Task DeleteByIdAsync(string table, string id) => DeleteAsync(table, $"id=eq.{Uri.EscapeDataString(id)}");
    public Task DeleteWhereAsync(string table, string column, string value) => DeleteAsync(table, $"{column}=eq.{Uri.EscapeDataString(value)}");

    public async Task<TResult?> RpcAsync<TResult>(string function, object? args = null)
    {
        var json = await SendAsync(HttpMethod.Post, $"{RestUrl}/rpc/{function}", args ?? new { });
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<TResult>(json, JsonOptions);
    }

    public Task RpcAsync(string function, object? args = null) => RpcAsync<JsonElement?>(function, args);

    private static string TranslateError(HttpStatusCode status, string body)
    {
        string? detail = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var m)) detail = m.GetString();
            else if (doc.RootElement.TryGetProperty("hint", out var h)) detail = h.GetString();
        }
        catch { }

        if (!string.IsNullOrEmpty(detail) && !detail.Contains("row-level security") && !detail.Contains("violates"))
        {
            return detail;
        }

        return status switch
        {
            HttpStatusCode.Unauthorized => "Tu sesión caducó. Volvé a iniciar sesión.",
            HttpStatusCode.Forbidden => "No tenés permiso para hacer esto.",
            HttpStatusCode.NotFound => "El servidor no encuentra ese recurso. ¿Corriste el script SQL en Supabase?",
            HttpStatusCode.Conflict => "Ese dato ya existe o choca con otro registro.",
            _ when !string.IsNullOrEmpty(detail) && detail.Contains("row-level security") => "No tenés permiso para modificar ese dato.",
            _ => $"Error del servidor ({(int)status}). {detail}".Trim()
        };
    }
}
