using AppCobros.Models;

namespace AppCobros.Services;

public interface IDataService
{
    Task<CobrosData> LoadDataAsync();
    /// Devuelve el aviso de datos dañados (si lo hubo al cargar) y lo limpia para que se muestre una sola vez.
    string? TomarAvisoDatosDanados();
    Task SaveDataAsync(CobrosData data);
    Task ImportDataAsync(string json);
    Task<string> ExportDataAsync();
}
