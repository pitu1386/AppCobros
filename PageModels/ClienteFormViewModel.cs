using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppCobros.Models;
using AppCobros.Services;
using AppCobros.Utilities;

namespace AppCobros.PageModels;

[QueryProperty(nameof(ClientId), "ClientId")]
[QueryProperty(nameof(PreGrupoId), "PreGrupoId")]
public partial class ClienteFormViewModel : BaseViewModel
{
    private readonly IDataService _dataService;
    private CobrosData? _data;

    [ObservableProperty] private int _clientId;
    [ObservableProperty] private int _preGrupoId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CuotaLabel))]
    private string _nombre = string.Empty;

    [ObservableProperty]
    private string _telefono = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CuotaLabel))]
    private Grupo? _grupoSeleccionado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CuotaLabel))]
    private int _anexos;

    [ObservableProperty]
    private bool _mesVencido;

    public ObservableCollection<Grupo> Grupos { get; } = new();

    public bool IsEditing => ClientId > 0;
    public bool CanDelete => IsEditing;

    public string CuotaLabel
    {
        get
        {
            if (_data == null) return string.Empty;
            double cuota = (GrupoSeleccionado?.Cuota ?? 0) + (Anexos * _data.Config.Anexo);
            return $"Cuota mensual resultante: {CobrosHelper.FormatMoney(cuota)}";
        }
    }

    public ClienteFormViewModel(IDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        _data = await _dataService.LoadDataAsync();

        Grupos.Clear();
        foreach (var g in _data.Grupos)
            Grupos.Add(g);

        if (ClientId > 0)
        {
            // Editando cliente existente
            Title = "Editar cliente";
            var c = _data.Clients.FirstOrDefault(x => x.Id == ClientId);
            if (c != null)
            {
                Nombre = c.Nombre;
                Telefono = c.Telefono;
                Anexos = c.Anexos;
                MesVencido = c.MesVencido;
                GrupoSeleccionado = Grupos.FirstOrDefault(g => g.Id == c.GrupoId) ?? Grupos.FirstOrDefault();
            }
        }
        else
        {
            // Nuevo cliente
            Title = "Nuevo cliente";
            Nombre = string.Empty;
            Telefono = string.Empty;
            Anexos = 0;
            MesVencido = false;
            GrupoSeleccionado = Grupos.FirstOrDefault(g => g.Id == PreGrupoId) ?? Grupos.FirstOrDefault();
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        if (string.IsNullOrWhiteSpace(Nombre)) return;
        if (_data == null) return;

        int grupoId = GrupoSeleccionado?.Id ?? 0;

        if (IsEditing)
        {
            var c = _data.Clients.FirstOrDefault(x => x.Id == ClientId);
            if (c != null)
            {
                c.Nombre = Nombre.Trim();
                c.Telefono = Telefono.Trim();
                c.GrupoId = grupoId;
                c.Anexos = Anexos;
                c.MesVencido = MesVencido;
            }
        }
        else
        {
            var nuevo = new Client
            {
                Id = _data.NextId++,
                Nombre = Nombre.Trim(),
                Telefono = Telefono.Trim(),
                GrupoId = grupoId,
                Anexos = Anexos,
                MesVencido = MesVencido
            };
            _data.Clients.Add(nuevo);
            ClientId = nuevo.Id;
        }

        await _dataService.SaveDataAsync(_data);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task BorrarAsync()
    {
        if (!IsEditing || _data == null) return;

        var c = _data.Clients.FirstOrDefault(x => x.Id == ClientId);
        if (c == null) return;

        if (!c.Archivado)
        {
            await Shell.Current.DisplayAlertAsync("Archivá primero", "Para eliminar un cliente definitivamente primero tenés que archivarlo desde su ficha. Así evitamos borrar historial por error.", "OK");
            return;
        }

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Eliminar definitivamente",
            "¿Eliminar este cliente y todo su historial para siempre? Esta acción no se puede deshacer.",
            "Eliminar", "Cancelar");

        if (!confirm) return;

        _data.Clients.Remove(c);

        await _dataService.SaveDataAsync(_data);
        // Volver 2 niveles: formulario → detalle → lista
        await Shell.Current.GoToAsync("../..");
    }
}
