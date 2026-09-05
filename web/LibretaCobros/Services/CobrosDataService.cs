using System.Text.Json;
using LibretaCobros.Models;
using LibretaCobros.Utilities;
using Microsoft.JSInterop;

namespace LibretaCobros.Services;

/// <summary>
/// Estado de la libreta en memoria + caché en localStorage para pintar al instante.
/// La verdad vive en Supabase: cada mutación escribe la/las fila(s) afectada(s), vuelve a leer
/// la tabla y avisa de errores por <see cref="OnError"/>. Los cambios de otros dispositivos
/// (PC ↔ celular) llegan por Realtime.
/// </summary>
public class CobrosDataService : ICobrosDataService, IDisposable
{
    private const string CachePrefix = "lc2_";
    private static readonly string[] AllTables = { "config", "grupos", "clientes", "movimientos", "papelera" };

    private readonly IJSRuntime _js;
    private readonly SupabaseAuthService _auth;
    private readonly SupabaseClientService _sb;
    private DotNetObjectReference<CobrosDataService>? _selfRef;
    private bool _initialized;

    public event Action? OnChange;
    public event Action<string>? OnError;

    private Config _config = new();
    private List<Grupo> _grupos = new();
    private List<Cliente> _clientes = new();
    private List<Movimiento> _movimientos = new();
    private List<MovimientoEliminado> _papelera = new();

    public CobrosDataService(IJSRuntime js, SupabaseAuthService auth, SupabaseClientService sb)
    {
        _js = js;
        _auth = auth;
        _sb = sb;
        _auth.OnSessionChanged += HandleSessionChanged;
    }

    // ── Sesión ──────────────────────────────────────────────
    public bool IsAuthenticated => _auth.IsSignedIn;
    public string? SessionEmail => _auth.Email;

    public async Task<(bool Success, string Error)> LoginAsync(string email, string password)
    {
        var (ok, err) = await _auth.SignInWithPasswordAsync(email.Trim(), password);
        if (ok)
        {
            await LoadCacheAsync();
            await RefreshFromCloudAsync();
            await StartRealtimeAsync();
        }
        return (ok, err);
    }

    public Task<(bool Success, string Error)> ChangePasswordAsync(string newPassword) =>
        _auth.UpdatePasswordAsync(newPassword);

    public async Task LogoutAsync()
    {
        await StopRealtimeAsync();
        await _auth.SignOutAsync();
        _config = new();
        _grupos = new();
        _clientes = new();
        _movimientos = new();
        _papelera = new();
        NotifyChanged();
    }

    // ── Estado ──────────────────────────────────────────────
    public Config Config => _config;
    public IReadOnlyList<Grupo> Grupos => _grupos;
    public IReadOnlyList<Cliente> Clientes => _clientes;
    public IReadOnlyList<MovimientoEliminado> Papelera => _papelera;

    public bool IsCloudConnected { get; private set; }
    public bool IsRealtimeConnected { get; private set; }
    public DateTime? LastSyncUtc { get; private set; }

    public Cliente? GetCliente(string id) => _clientes.FirstOrDefault(c => c.Id == id);
    public Grupo? GetGrupo(string id) => _grupos.FirstOrDefault(g => g.Id == id);

    // ── Arranque ────────────────────────────────────────────
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        await _auth.LoadAsync();
        if (_auth.IsSignedIn)
        {
            await LoadCacheAsync();
            NotifyChanged();
            await RefreshFromCloudAsync();
            await StartRealtimeAsync();
        }
        NotifyChanged();
    }

    public async Task RefreshFromCloudAsync()
    {
        if (!_auth.IsSignedIn) return;
        var results = await Task.WhenAll(AllTables.Select(RefreshTableCoreAsync));
        AttachMovimientos();
        if (results.Any(r => r)) { IsCloudConnected = true; LastSyncUtc = DateTime.UtcNow; }
        if (results.All(r => !r) && _lastError != null) OnError?.Invoke(_lastError);
        await PurgarPapeleraAsync();
        NotifyChanged();
    }

    private string? _lastError;

    private async Task<bool> RefreshTableCoreAsync(string table)
    {
        try
        {
            switch (table)
            {
                case "config":
                    var rows = await _sb.GetAsync<Config>("config", "id=eq.current&select=*");
                    if (rows.Count > 0) _config = rows[0];
                    break;
                case "grupos":
                    _grupos = await _sb.GetAsync<Grupo>("grupos", "select=*&order=nombre");
                    break;
                case "clientes":
                    _clientes = await _sb.GetAsync<Cliente>("clientes", "select=*&order=nombre");
                    break;
                case "movimientos":
                    _movimientos = await _sb.GetAsync<Movimiento>("movimientos", "select=*");
                    break;
                case "papelera":
                    _papelera = await _sb.GetAsync<MovimientoEliminado>("papelera", "select=*");
                    break;
                default:
                    return false;
            }
            await SaveCacheAsync(table);
            return true;
        }
        catch (SupabaseException ex)
        {
            _lastError = ex.Message;
            if (ex.StatusCode == null) IsCloudConnected = false;
            return false;
        }
        catch (Exception ex)
        {
            _lastError = $"Error leyendo {table}: {ex.Message}";
            return false;
        }
    }

    private async Task RefreshTablesAsync(params string[] tables)
    {
        var results = await Task.WhenAll(tables.Select(RefreshTableCoreAsync));
        AttachMovimientos();
        if (results.Any(r => r)) { IsCloudConnected = true; LastSyncUtc = DateTime.UtcNow; }
        NotifyChanged();
    }

    private void AttachMovimientos()
    {
        foreach (var c in _clientes)
            c.Movimientos = _movimientos
                .Where(m => m.ClienteId == c.Id)
                .OrderBy(m => m.Fecha).ThenBy(m => m.Id)
                .ToList();
    }

    private async Task<bool> WriteAsync(Func<Task> op, params string[] refreshTables)
    {
        bool ok;
        try
        {
            await op();
            IsCloudConnected = true;
            ok = true;
        }
        catch (SupabaseException ex)
        {
            if (ex.StatusCode == null) IsCloudConnected = false;
            OnError?.Invoke(ex.Message);
            ok = false;
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Error inesperado: {ex.Message}");
            ok = false;
        }
        // Se refresca también tras un fallo para deshacer cualquier cambio optimista local.
        await RefreshTablesAsync(refreshTables.Length > 0 ? refreshTables : AllTables);
        return ok;
    }

    // ── Config ──────────────────────────────────────────────
    public Task SaveConfigAsync(Config config)
    {
        config.Id = "current";
        _config = config;
        return WriteAsync(() => _sb.UpsertRowAsync("config", config), "config");
    }

    public Task<bool> SaveAjustesAsync(Config config, List<Grupo> grupos)
    {
        config.Id = "current";
        foreach (var g in grupos)
        {
            if (string.IsNullOrEmpty(g.Id)) g.Id = NewId("g");
            var prev = GetGrupo(g.Id);
            if (prev != null && Math.Abs(prev.Cuota - g.Cuota) > 0.001)
                g.HistorialCuota = new List<CuotaHistorialEntry>(g.HistorialCuota)
                {
                    new() { Fecha = CobrosHelper.HoyISO(), Cuota = g.Cuota }
                };
        }

        var eliminados = _grupos.Where(og => grupos.All(g => g.Id != og.Id)).Select(og => og.Id).ToList();
        var conClientes = eliminados.Where(id => _clientes.Any(c => c.GrupoId == id)).ToList();
        if (conClientes.Count > 0)
        {
            OnError?.Invoke("No se puede borrar un grupo que todavía tiene clientes.");
            return Task.FromResult(false);
        }

        _config = config;
        return WriteAsync(async () =>
        {
            await _sb.UpsertRowAsync("config", config);
            if (grupos.Count > 0) await _sb.UpsertAsync("grupos", grupos);
            foreach (var id in eliminados) await _sb.DeleteByIdAsync("grupos", id);
        }, "config", "grupos");
    }

    // ── Grupos ──────────────────────────────────────────────
    public Task<bool> SaveGrupoAsync(Grupo grupo)
    {
        if (string.IsNullOrEmpty(grupo.Id)) grupo.Id = NewId("g");
        else
        {
            var prev = GetGrupo(grupo.Id);
            if (prev != null && Math.Abs(prev.Cuota - grupo.Cuota) > 0.001)
            {
                grupo.HistorialCuota = new List<CuotaHistorialEntry>(grupo.HistorialCuota)
                {
                    new() { Fecha = CobrosHelper.HoyISO(), Cuota = grupo.Cuota }
                };
            }
        }
        return WriteAsync(() => _sb.UpsertRowAsync("grupos", grupo), "grupos");
    }

    public Task<bool> DeleteGrupoAsync(string id)
    {
        if (_clientes.Any(c => c.GrupoId == id))
        {
            OnError?.Invoke("No se puede borrar: hay clientes en ese grupo.");
            return Task.FromResult(false);
        }
        return WriteAsync(() => _sb.DeleteByIdAsync("grupos", id), "grupos");
    }

    // ── Clientes ────────────────────────────────────────────
    public Task<bool> SaveClienteAsync(Cliente cliente)
    {
        if (string.IsNullOrEmpty(cliente.Id)) cliente.Id = NewId("c");
        return WriteAsync(() => _sb.UpsertRowAsync("clientes", cliente), "clientes");
    }

    public Task<bool> SetClienteArchivadoAsync(string id, bool archivado)
    {
        var c = GetCliente(id);
        if (c == null) return Task.FromResult(false);
        c.Archivado = archivado;
        return WriteAsync(() => _sb.UpsertRowAsync("clientes", c), "clientes");
    }

    public Task<bool> DeleteClienteAsync(string id) => WriteAsync(async () =>
    {
        await _sb.DeleteWhereAsync("movimientos", "cliente_id", id);
        await _sb.DeleteByIdAsync("clientes", id);
    }, "clientes", "movimientos");

    // ── Movimientos ─────────────────────────────────────────
    public Task<bool> RegistrarCargoAsync(string clienteId, string concepto, double monto, string fecha, string? mes)
    {
        var mov = new Movimiento
        {
            Id = NewId("m"),
            ClienteId = clienteId,
            Tipo = "cargo",
            Fecha = fecha,
            Mes = string.IsNullOrEmpty(mes) ? null : mes,
            Concepto = concepto,
            Monto = monto
        };
        var cliente = GetCliente(clienteId);
        var tocaCliente = mov.Mes != null && cliente != null && !cliente.Meses.Contains(mov.Mes);

        return WriteAsync(async () =>
        {
            await _sb.UpsertRowAsync("movimientos", mov);
            if (tocaCliente)
            {
                cliente!.Meses = new List<string>(cliente.Meses) { mov.Mes! };
                await _sb.UpsertRowAsync("clientes", cliente);
            }
        }, "movimientos", "clientes");
    }

    public Task<bool> RegistrarPagoAsync(string clienteId, double monto, string fecha, double? cotizacionEuro)
    {
        var mov = new Movimiento
        {
            Id = NewId("m"),
            ClienteId = clienteId,
            Tipo = "pago",
            Fecha = fecha,
            Concepto = "Pago recibido",
            Monto = monto,
            CotizacionEuro = cotizacionEuro
        };
        return WriteAsync(() => _sb.UpsertRowAsync("movimientos", mov), "movimientos");
    }

    public Task<bool> ActualizarMovimientoAsync(Movimiento movimiento) =>
        WriteAsync(() => _sb.UpsertRowAsync("movimientos", movimiento), "movimientos", "clientes");

    public Task<bool> EliminarMovimientoAsync(string clienteId, string movimientoId)
    {
        var cliente = GetCliente(clienteId);
        var mov = _movimientos.FirstOrDefault(m => m.Id == movimientoId);
        if (mov == null) return Task.FromResult(false);

        string? mesLiberado = null;
        if (mov.Tipo == "cargo" && !string.IsNullOrEmpty(mov.Mes) && cliente != null && cliente.Meses.Contains(mov.Mes))
            mesLiberado = mov.Mes;

        var eliminado = new MovimientoEliminado
        {
            Id = mov.Id,
            ClienteId = clienteId,
            ClienteNombre = cliente?.Nombre ?? "",
            EliminadoEl = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            MesLiberado = mesLiberado,
            Movimiento = mov
        };

        return WriteAsync(async () =>
        {
            await _sb.UpsertRowAsync("papelera", eliminado);
            await _sb.DeleteByIdAsync("movimientos", movimientoId);
            if (mesLiberado != null && cliente != null)
            {
                cliente.Meses = cliente.Meses.Where(x => x != mesLiberado).ToList();
                await _sb.UpsertRowAsync("clientes", cliente);
            }
        }, "movimientos", "papelera", "clientes");
    }

    public async Task<int> GenerarCuotasMesAsync(string mesKey)
    {
        var nuevos = new List<Movimiento>();
        var clientesTocados = new List<Cliente>();
        var label = CobrosHelper.MesLabel(mesKey);
        var hoy = CobrosHelper.HoyISO();

        foreach (var c in _clientes.Where(c => !c.Archivado))
        {
            if (CobrosHelper.FacturadoMes(c, mesKey)) continue;
            var cuota = CobrosHelper.CuotaDe(c, _grupos, _config);
            if (cuota <= 0) continue;

            nuevos.Add(new Movimiento
            {
                Id = NewId("m"),
                ClienteId = c.Id,
                Tipo = "cargo",
                Fecha = hoy,
                Mes = mesKey,
                Concepto = $"Cuota {label}",
                Monto = cuota
            });
            c.Meses = new List<string>(c.Meses) { mesKey };
            clientesTocados.Add(c);
        }

        if (nuevos.Count == 0) return 0;

        await WriteAsync(async () =>
        {
            await _sb.UpsertAsync("movimientos", nuevos);
            await _sb.UpsertAsync("clientes", clientesTocados);
        }, "movimientos", "clientes");

        return nuevos.Count;
    }

    // ── Reclamos ────────────────────────────────────────────
    public Task<bool> MarcarReclamadoAsync(IEnumerable<string> clienteIds, string fecha)
    {
        var set = clienteIds.ToHashSet();
        var tocados = _clientes.Where(c => set.Contains(c.Id)).ToList();
        foreach (var c in tocados) c.UltRec = fecha;
        if (tocados.Count == 0) return Task.FromResult(true);
        return WriteAsync(() => _sb.UpsertAsync("clientes", tocados), "clientes");
    }

    // ── Papelera ────────────────────────────────────────────
    public Task<bool> RestaurarDePapeleraAsync(string movimientoId)
    {
        var el = _papelera.FirstOrDefault(e => e.Id == movimientoId);
        if (el == null) return Task.FromResult(false);
        var cliente = GetCliente(el.ClienteId);

        return WriteAsync(async () =>
        {
            await _sb.UpsertRowAsync("movimientos", el.Movimiento);
            if (!string.IsNullOrEmpty(el.MesLiberado) && cliente != null && !cliente.Meses.Contains(el.MesLiberado))
            {
                cliente.Meses = new List<string>(cliente.Meses) { el.MesLiberado };
                await _sb.UpsertRowAsync("clientes", cliente);
            }
            await _sb.DeleteByIdAsync("papelera", movimientoId);
        }, "movimientos", "papelera", "clientes");
    }

    public Task<bool> DescartarDePapeleraAsync(string movimientoId) =>
        WriteAsync(() => _sb.DeleteByIdAsync("papelera", movimientoId), "papelera");

    public Task<bool> VaciarPapeleraAsync()
    {
        var ids = _papelera.Select(e => e.Id).ToList();
        if (ids.Count == 0) return Task.FromResult(true);
        return WriteAsync(async () =>
        {
            foreach (var id in ids) await _sb.DeleteByIdAsync("papelera", id);
        }, "papelera");
    }

    private async Task PurgarPapeleraAsync()
    {
        try
        {
            var vencidos = CobrosHelper.VencidosDePapelera(_papelera);
            foreach (var v in vencidos) await _sb.DeleteByIdAsync("papelera", v.Id);
            if (vencidos.Count > 0) await RefreshTableCoreAsync("papelera");
        }
        catch { /* la purga nunca debe romper el arranque */ }
    }

    // ── Realtime (puente JS) ────────────────────────────────
    private async Task StartRealtimeAsync()
    {
        try
        {
            _selfRef ??= DotNetObjectReference.Create(this);
            var token = await _auth.GetAccessTokenAsync();
            await _js.InvokeVoidAsync("lcRealtime.start", AppInfo.SupabaseUrl, AppInfo.SupabaseAnonKey, token, _selfRef);
        }
        catch { IsRealtimeConnected = false; }
    }

    private async Task StopRealtimeAsync()
    {
        try { await _js.InvokeVoidAsync("lcRealtime.stop"); } catch { }
        IsRealtimeConnected = false;
    }

    private async void HandleSessionChanged()
    {
        if (!_auth.IsSignedIn) return;
        try
        {
            var token = await _auth.GetAccessTokenAsync();
            if (token != null) await _js.InvokeVoidAsync("lcRealtime.setAuth", token);
        }
        catch { }
    }

    [JSInvokable]
    public async Task OnCloudChange(string table)
    {
        if (!_auth.IsSignedIn) return;
        if (AllTables.Contains(table)) await RefreshTablesAsync(table);
        else await RefreshFromCloudAsync();
    }

    [JSInvokable]
    public Task OnRealtimeStatus(bool connected)
    {
        IsRealtimeConnected = connected;
        NotifyChanged();
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnAppResumed() => RefreshFromCloudAsync();

    // ── Caché local ─────────────────────────────────────────
    private async Task LoadCacheAsync()
    {
        try
        {
            _config = await ReadCacheAsync<Config>("config") ?? new();
            _grupos = await ReadCacheAsync<List<Grupo>>("grupos") ?? new();
            _clientes = await ReadCacheAsync<List<Cliente>>("clientes") ?? new();
            _movimientos = await ReadCacheAsync<List<Movimiento>>("movimientos") ?? new();
            _papelera = await ReadCacheAsync<List<MovimientoEliminado>>("papelera") ?? new();
            AttachMovimientos();
        }
        catch { /* caché corrupta: se ignora, la nube manda */ }
    }

    private async Task<T?> ReadCacheAsync<T>(string table)
    {
        var raw = await _js.InvokeAsync<string?>("blazorLocalStorage.get", CachePrefix + table);
        return string.IsNullOrEmpty(raw) ? default : JsonSerializer.Deserialize<T>(raw, SupabaseClientService.JsonOptions);
    }

    private async Task SaveCacheAsync(string table)
    {
        try
        {
            object payload = table switch
            {
                "config" => _config,
                "grupos" => _grupos,
                "clientes" => _clientes,
                "movimientos" => _movimientos,
                "papelera" => _papelera,
                _ => new { }
            };
            var json = JsonSerializer.Serialize(payload, SupabaseClientService.JsonOptions);
            await _js.InvokeVoidAsync("blazorLocalStorage.set", CachePrefix + table, json);
        }
        catch { }
    }

    // ── Utilidades ──────────────────────────────────────────
    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..14];

    private void NotifyChanged() => OnChange?.Invoke();

    public void Dispose()
    {
        _auth.OnSessionChanged -= HandleSessionChanged;
        _selfRef?.Dispose();
    }
}
