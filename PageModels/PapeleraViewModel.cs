using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppCobros.Models;
using AppCobros.Services;
using AppCobros.Utilities;

namespace AppCobros.PageModels;

public partial class PapeleraViewModel : BaseViewModel
{
    private readonly IDataService _dataService;
    private CobrosData? _data;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayElementos))]
    [NotifyPropertyChangedFor(nameof(EstaVacia))]
    private ObservableCollection<MovimientoEliminado> _eliminados = new();

    public bool HayElementos => Eliminados.Count > 0;
    public bool EstaVacia => Eliminados.Count == 0;

    [ObservableProperty]
    private string _resumen = string.Empty;

    public PapeleraViewModel(IDataService dataService)
    {
        _dataService = dataService;
        Title = "Papelera";
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        _data = await _dataService.LoadDataAsync();

        // Al abrir aprovechamos para descartar lo que ya venció.
        CobrosHelper.PurgarPapelera(_data);

        Eliminados = new ObservableCollection<MovimientoEliminado>(_data.Papelera);
        Resumen = Eliminados.Count == 0
            ? $"No hay movimientos eliminados. Lo que borres se guarda acá {CobrosHelper.DiasRetencionPapelera} días."
            : $"{Eliminados.Count} movimiento(s) eliminado(s). Se descartan solos a los {CobrosHelper.DiasRetencionPapelera} días.";

        IsBusy = false;
    }

    [RelayCommand]
    private async Task RestaurarAsync(MovimientoEliminado eliminado)
    {
        if (_data == null || eliminado == null) return;

        if (!CobrosHelper.RestaurarDePapelera(_data, eliminado))
        {
            await Shell.Current.DisplayAlertAsync("No se pudo restaurar", "El cliente de ese movimiento ya no existe.", "OK");
            return;
        }

        await _dataService.SaveDataAsync(_data);
        await LoadDataAsync();
        await AppShell.DisplayToastAsync("Movimiento restaurado");
    }

    [RelayCommand]
    private async Task EliminarDefinitivoAsync(MovimientoEliminado eliminado)
    {
        if (_data == null || eliminado == null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Eliminar definitivamente",
            "Este movimiento se borra para siempre. ¿Confirmás?",
            "Eliminar", "Cancelar");
        if (!confirm) return;

        _data.Papelera.Remove(eliminado);
        await _dataService.SaveDataAsync(_data);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task VaciarAsync()
    {
        if (_data == null || _data.Papelera.Count == 0) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Vaciar papelera",
            $"Se van a borrar para siempre {_data.Papelera.Count} movimiento(s). Esta acción no se puede deshacer.",
            "Vaciar", "Cancelar");
        if (!confirm) return;

        _data.Papelera.Clear();
        await _dataService.SaveDataAsync(_data);
        await LoadDataAsync();
    }
}
