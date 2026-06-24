using System.Text.Json;
using AppCobros.Models;

namespace AppCobros.Services;

public class DataService : IDataService
{
    private readonly string _filePath;
    
    // We cache the instance so changes to ObservableCollections are tracked
    private CobrosData? _cachedData;

    public DataService()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "data.json");
    }

    public async Task<CobrosData> LoadDataAsync()
    {
        if (_cachedData != null)
            return _cachedData;

        if (!File.Exists(_filePath))
        {
            _cachedData = new CobrosData();
            // Default groups from React app
            _cachedData.Grupos.Add(new Grupo { Id = 1, Nombre = "1 negocio", Cuota = 32000 });
            _cachedData.Grupos.Add(new Grupo { Id = 2, Nombre = "2 negocios", Cuota = 54400 });
            _cachedData.Grupos.Add(new Grupo { Id = 3, Nombre = "3 o más negocios", Cuota = 64000 });
            await SaveDataAsync(_cachedData);
            return _cachedData;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            _cachedData = JsonSerializer.Deserialize<CobrosData>(json) ?? new CobrosData();
            return _cachedData;
        }
        catch
        {
            _cachedData = new CobrosData();
            return _cachedData;
        }
    }

    public async Task SaveDataAsync(CobrosData data)
    {
        _cachedData = data;
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task ImportDataAsync(string json)
    {
        var data = JsonSerializer.Deserialize<CobrosData>(json);
        if (data != null && data.Clients != null)
        {
            await SaveDataAsync(data);
        }
        else
        {
            throw new Exception("Formato JSON inválido");
        }
    }

    public async Task<string> ExportDataAsync()
    {
        var data = await LoadDataAsync();
        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }
}
