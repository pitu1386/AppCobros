using System.Text.Json;
using AppCobros.Models;

namespace AppCobros.Services;

public class DataService : IDataService
{
    private readonly string _filePath;
    private readonly string _backupDir;
    private const int MaxBackups = 7;

    // We cache the instance so changes to ObservableCollections are tracked
    private CobrosData? _cachedData;

    // Si la carga falló por archivo dañado, guardamos el aviso para que la UI lo muestre
    public string? AvisoDatosDanados { get; private set; }

    public string? TomarAvisoDatosDanados()
    {
        var aviso = AvisoDatosDanados;
        AvisoDatosDanados = null;
        return aviso;
    }

    public DataService()
    {
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "data.json");
        _backupDir = Path.Combine(FileSystem.AppDataDirectory, "backups");
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
            WidgetService.Actualizar(_cachedData);
            return _cachedData;
        }
        catch
        {
            // Nunca pisar los datos dañados: los apartamos para poder recuperarlos a mano
            // y avisamos en la UI en vez de continuar en silencio con una libreta vacía.
            var rescate = Path.Combine(FileSystem.AppDataDirectory, $"data.corrupto_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            try { File.Copy(_filePath, rescate, overwrite: true); } catch { }

            var backup = UltimoBackupDisponible();
            if (backup != null)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(backup);
                    _cachedData = JsonSerializer.Deserialize<CobrosData>(json);
                    if (_cachedData != null)
                    {
                        AvisoDatosDanados = $"El archivo de datos estaba dañado. Se restauró la copia automática del {File.GetLastWriteTime(backup):dd/MM/yyyy HH:mm}.";
                        return _cachedData;
                    }
                }
                catch { }
            }

            AvisoDatosDanados = "El archivo de datos estaba dañado y no había copia automática. Se guardó una copia del archivo dañado en la carpeta de la app. Podés restaurar un backup desde Ajustes.";
            _cachedData = new CobrosData();
            return _cachedData;
        }
    }

    public async Task SaveDataAsync(CobrosData data)
    {
        _cachedData = data;
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

        // Escritura atómica: primero a un temporal y después reemplazamos,
        // así un cierre a mitad de guardado no deja el archivo por la mitad.
        var tmpPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tmpPath, json);
        if (File.Exists(_filePath))
            File.Replace(tmpPath, _filePath, destinationBackupFileName: null);
        else
            File.Move(tmpPath, _filePath);

        BackupAutomatico(json);
        WidgetService.Actualizar(data);
    }

    // Una copia por día, rotando las últimas MaxBackups
    private void BackupAutomatico(string json)
    {
        try
        {
            Directory.CreateDirectory(_backupDir);
            var backupPath = Path.Combine(_backupDir, $"auto_{DateTime.Now:yyyyMMdd}.json");
            File.WriteAllText(backupPath, json);
            Preferences.Default.Set("ultimo_backup_auto", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));

            var viejos = Directory.GetFiles(_backupDir, "auto_*.json")
                .OrderByDescending(f => f)
                .Skip(MaxBackups);
            foreach (var f in viejos)
                File.Delete(f);
        }
        catch
        {
            // El backup nunca debe impedir el guardado principal
        }
    }

    private string? UltimoBackupDisponible()
    {
        if (!Directory.Exists(_backupDir)) return null;
        return Directory.GetFiles(_backupDir, "auto_*.json").OrderByDescending(f => f).FirstOrDefault();
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
