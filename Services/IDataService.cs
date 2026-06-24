using AppCobros.Models;

namespace AppCobros.Services;

public interface IDataService
{
    Task<CobrosData> LoadDataAsync();
    Task SaveDataAsync(CobrosData data);
    Task ImportDataAsync(string json);
    Task<string> ExportDataAsync();
}
