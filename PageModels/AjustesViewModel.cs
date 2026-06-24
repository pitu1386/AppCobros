using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppCobros.Models;
using AppCobros.Services;
using AppCobros.Utilities;

namespace AppCobros.PageModels;

public partial class AjustesViewModel : BaseViewModel
{
    private readonly IDataService _dataService;
    private CobrosData? _data;

    [ObservableProperty]
    private Config _config = new();

    [ObservableProperty]
    private ObservableCollection<Grupo> _grupos = new();

    public AjustesViewModel(IDataService dataService)
    {
        _dataService = dataService;
        Title = "Ajustes";
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        _data = await _dataService.LoadDataAsync();
        
        Config = _data.Config;
        Grupos = new ObservableCollection<Grupo>(_data.Grupos);

        IsBusy = false;
    }

    [RelayCommand]
    private void AgregarGrupo()
    {
        if (_data == null) return;
        Grupos.Add(new Grupo { Id = _data.NextGid, Nombre = "Nuevo grupo", Cuota = 0 });
        _data.NextGid++;
    }

    [RelayCommand]
    private async Task EliminarGrupoAsync(Grupo g)
    {
        if (_data == null) return;
        int enUso = _data.Clients.Count(c => c.GrupoId == g.Id);
        if (enUso > 0)
        {
            await Shell.Current.DisplayAlertAsync("Error", $"No se puede eliminar: hay {enUso} cliente(s) en este grupo. Movelos a otro grupo primero.", "OK");
            return;
        }
        Grupos.Remove(g);
    }

    [ObservableProperty]
    private string _importText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayExportText))]
    private string _exportText = string.Empty;

    public bool HayExportText => !string.IsNullOrEmpty(ExportText);

    [RelayCommand]
    private async Task GuardarAjustesAsync()
    {
        if (_data == null) return;
        IsBusy = true;
        _data.Config = Config;
        _data.Grupos = new ObservableCollection<Grupo>(Grupos);
        await _dataService.SaveDataAsync(_data);
        IsBusy = false;
        await Shell.Current.DisplayAlertAsync("Éxito", "Ajustes guardados correctamente.", "OK");
    }

    [RelayCommand]
    private async Task ExportarJsonAsync()
    {
        IsBusy = true;
        ExportText = await _dataService.ExportDataAsync();
        IsBusy = false;
        await Shell.Current.DisplayAlertAsync("Éxito", "Datos listos para exportar. Podés copiar el texto de abajo.", "OK");
    }

    [RelayCommand]
    private async Task CopiarExportAsync()
    {
        if (string.IsNullOrWhiteSpace(ExportText)) return;
        await Clipboard.Default.SetTextAsync(ExportText);
        await Shell.Current.DisplayAlertAsync("Copiado", "El JSON se ha copiado al portapapeles.", "OK");
    }

    [RelayCommand]
    private async Task ImportarJsonAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportText))
        {
            await Shell.Current.DisplayAlertAsync("Error", "Pegá el texto JSON primero.", "OK");
            return;
        }

        bool confirm = await Shell.Current.DisplayAlertAsync("Importar", "Esto reemplazará todos los datos actuales. ¿Estás seguro?", "Sí, reemplazar", "Cancelar");
        if (confirm)
        {
            try
            {
                IsBusy = true;
                await _dataService.ImportDataAsync(ImportText);
                ImportText = string.Empty;
                await LoadDataAsync();
                await Shell.Current.DisplayAlertAsync("Éxito", "Datos importados correctamente. Ve a Inicio para verlos.", "OK");
            }
            catch
            {
                IsBusy = false;
                await Shell.Current.DisplayAlertAsync("Error", "El texto pegado no tiene el formato correcto.", "OK");
            }
        }
    }

    [RelayCommand]
    private async Task ExportarArchivoAsync()
    {
        IsBusy = true;
        try
        {
            string json = await _dataService.ExportDataAsync();
            string fileName = $"CobrosBackup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            File.WriteAllText(filePath, json);

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Copia de Seguridad de Cobros",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception)
        {
            await Shell.Current.DisplayAlertAsync("Error", "No se pudo exportar el archivo.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportarArchivoAsync()
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona tu copia de seguridad (.json)"
            });

            if (result != null)
            {
                if (result.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    bool confirm = await Shell.Current.DisplayAlertAsync("Restaurar Backup", "Esto reemplazará todos los datos actuales con los del archivo. ¿Estás seguro?", "Sí, reemplazar", "Cancelar");
                    if (confirm)
                    {
                        IsBusy = true;
                        string json = File.ReadAllText(result.FullPath);
                        await _dataService.ImportDataAsync(json);
                        await LoadDataAsync();
                        await Shell.Current.DisplayAlertAsync("Éxito", "Copia de seguridad restaurada correctamente.", "OK");
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlertAsync("Error", "El archivo debe tener extensión .json", "OK");
                }
            }
        }
        catch (Exception)
        {
            await Shell.Current.DisplayAlertAsync("Error", "Ocurrió un error al intentar leer el archivo.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
