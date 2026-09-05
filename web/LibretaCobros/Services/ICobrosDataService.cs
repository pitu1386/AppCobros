using LibretaCobros.Models;

namespace LibretaCobros.Services;

public interface ICobrosDataService
{
    /// <summary>Cambió el estado en memoria (local o desde la nube).</summary>
    event Action? OnChange;
    /// <summary>Una operación contra la nube falló. El texto es apto para mostrar al usuario.</summary>
    event Action<string>? OnError;

    Task InitializeAsync();

    // Sesión (login "solo yo")
    bool IsAuthenticated { get; }
    string? SessionEmail { get; }
    Task<(bool Success, string Error)> LoginAsync(string email, string password);
    Task<(bool Success, string Error)> ChangePasswordAsync(string newPassword);
    Task LogoutAsync();

    // Sincronización
    bool IsCloudConnected { get; }
    bool IsRealtimeConnected { get; }
    DateTime? LastSyncUtc { get; }
    Task RefreshFromCloudAsync();

    // Estado en memoria
    Config Config { get; }
    IReadOnlyList<Grupo> Grupos { get; }
    IReadOnlyList<Cliente> Clientes { get; }
    IReadOnlyList<MovimientoEliminado> Papelera { get; }

    Cliente? GetCliente(string id);
    Grupo? GetGrupo(string id);

    // Config
    Task SaveConfigAsync(Config config);

    // Ajustes: guarda config + grupos (alta/edición/baja) en una sola operación
    Task<bool> SaveAjustesAsync(Config config, List<Grupo> grupos);

    // Grupos
    Task<bool> SaveGrupoAsync(Grupo grupo);            // alta o edición (registra cambio de cuota en el historial)
    Task<bool> DeleteGrupoAsync(string id);

    // Clientes
    Task<bool> SaveClienteAsync(Cliente cliente);      // alta o edición
    Task<bool> SetClienteArchivadoAsync(string id, bool archivado);
    Task<bool> DeleteClienteAsync(string id);          // borra cliente y todos sus movimientos

    // Movimientos
    Task<bool> RegistrarCargoAsync(string clienteId, string concepto, double monto, string fecha, string? mes);
    Task<bool> RegistrarPagoAsync(string clienteId, double monto, string fecha, double? cotizacionEuro);
    Task<bool> ActualizarMovimientoAsync(Movimiento movimiento);
    Task<bool> EliminarMovimientoAsync(string clienteId, string movimientoId);
    /// <summary>Genera la cuota del mes para cada cliente activo que aún no la tenga. Devuelve cuántas creó.</summary>
    Task<int> GenerarCuotasMesAsync(string mesKey);

    // Reclamos
    Task<bool> MarcarReclamadoAsync(IEnumerable<string> clienteIds, string fecha);

    // Papelera
    Task<bool> RestaurarDePapeleraAsync(string movimientoId);
    Task<bool> DescartarDePapeleraAsync(string movimientoId);
    Task<bool> VaciarPapeleraAsync();
}
