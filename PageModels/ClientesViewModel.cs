using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppCobros.Models;
using AppCobros.Services;
using AppCobros.Utilities;

namespace AppCobros.PageModels;

public partial class ClientesViewModel : BaseViewModel
{
    private readonly IDataService _dataService;
    private CobrosData? _data;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<ClientGroupViewModel> GruposDeClientes { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OrdenLabel))]
    private string _ordenSort = "saldo";

    public string OrdenLabel => OrdenSort == "nombre" ? "Nombre A–Z" : "Saldo ↓";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FiltroEstadoLabel))]
    private string _filtroEstado = "todos";

    public string FiltroEstadoLabel => FiltroEstado switch
    {
        "deuda" => "Con deuda",
        "aldia" => "Al día",
        "afavor" => "A favor",
        _ => "Todos"
    };

    // 0 = todos los grupos. -1 = clientes sin grupo.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FiltroGrupoLabel))]
    private int _filtroGrupoId;

    public string FiltroGrupoLabel
    {
        get
        {
            if (FiltroGrupoId == 0) return "Todos";
            if (FiltroGrupoId == -1) return "Sin grupo";
            return _data?.Grupos.FirstOrDefault(g => g.Id == FiltroGrupoId)?.Nombre ?? "Todos";
        }
    }

    public ClientesViewModel(IDataService dataService)
    {
        _dataService = dataService;
        Title = "Clientes";
        _ordenSort = Preferences.Default.Get("clientes_orden", "saldo");
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        _data = await _dataService.LoadDataAsync();

        // Si el grupo filtrado se eliminó desde Ajustes, volvemos a "Todos".
        if (FiltroGrupoId > 0 && !_data.Grupos.Any(g => g.Id == FiltroGrupoId))
            FiltroGrupoId = 0;

        OnPropertyChanged(nameof(FiltroGrupoLabel));
        FilterClientes();
        IsBusy = false;
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterClientes();
    }

    [RelayCommand]
    private void ToggleSort()
    {
        OrdenSort = OrdenSort == "saldo" ? "nombre" : "saldo";
        Preferences.Default.Set("clientes_orden", OrdenSort);
        FilterClientes();
    }

    [RelayCommand]
    private void ToggleFiltroEstado()
    {
        FiltroEstado = FiltroEstado switch
        {
            "todos" => "deuda",
            "deuda" => "aldia",
            "aldia" => "afavor",
            _ => "todos"
        };
        FilterClientes();
    }

    [RelayCommand]
    private async Task ElegirGrupoAsync()
    {
        if (_data == null) return;

        const string todos = "Todos los grupos";
        const string sinGrupo = "Sin grupo";

        var opciones = new List<string> { todos };
        opciones.AddRange(_data.Grupos.Select(g => g.Nombre));
        opciones.Add(sinGrupo);

        string? elegido = await Shell.Current.DisplayActionSheetAsync("Filtrar por grupo", "Cancelar", null, opciones.ToArray());
        if (string.IsNullOrEmpty(elegido) || elegido == "Cancelar") return;

        FiltroGrupoId = elegido switch
        {
            todos => 0,
            sinGrupo => -1,
            _ => _data.Grupos.FirstOrDefault(g => g.Nombre == elegido)?.Id ?? 0
        };

        FilterClientes();
    }

    [RelayCommand]
    private async Task NuevoClienteAsync()
    {
        await Shell.Current.GoToAsync(nameof(ClienteFormPage));
    }

    [RelayCommand]
    private async Task NuevoClienteEnGrupoAsync(int grupoId)
    {
        await Shell.Current.GoToAsync($"{nameof(ClienteFormPage)}?PreGrupoId={grupoId}");
    }

    [RelayCommand]
    private async Task GoToClientAsync(int clientId)
    {
        await Shell.Current.GoToAsync($"{nameof(ClienteDetallePage)}?ClientId={clientId}");
    }

    private bool CumpleFiltroEstado(ClientItemViewModel vm) => FiltroEstado switch
    {
        "deuda" => vm.Saldo > 0,
        "aldia" => vm.Saldo == 0,
        "afavor" => vm.Saldo < 0,
        _ => true
    };

    private void FilterClientes()
    {
        if (_data == null) return;

        GruposDeClientes.Clear();
        var mesKey = CobrosHelper.MesKey(DateTime.Now);
        bool hayFiltrosActivos = !string.IsNullOrWhiteSpace(SearchText) || FiltroEstado != "todos" || FiltroGrupoId != 0;

        var activos = _data.Clients.Where(c => !c.Archivado).ToList();

        foreach (var g in _data.Grupos)
        {
            if (FiltroGrupoId != 0 && g.Id != FiltroGrupoId) continue;

            var clientes = activos.Where(c => c.GrupoId == g.Id).ToList();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                clientes = clientes.Where(c => c.Nombre.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var viewModels = clientes.Select(c => new ClientItemViewModel(c, _data.Grupos, _data.Config, mesKey))
                .Where(CumpleFiltroEstado)
                .ToList();

            if (viewModels.Count > 0 || !hayFiltrosActivos)
            {
                SortViewModels(viewModels);
                GruposDeClientes.Add(new ClientGroupViewModel(g.Nombre, g.Cuota, g.Id, viewModels));
            }
        }

        if (FiltroGrupoId == 0 || FiltroGrupoId == -1)
        {
            var sinGrupo = activos.Where(c => CobrosHelper.GrupoDe(c, _data.Grupos) == null).ToList();
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                sinGrupo = sinGrupo.Where(c => c.Nombre.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            var sinGrupoViewModels = sinGrupo.Select(c => new ClientItemViewModel(c, _data.Grupos, _data.Config, mesKey))
                .Where(CumpleFiltroEstado)
                .ToList();

            if (sinGrupoViewModels.Count > 0)
            {
                SortViewModels(sinGrupoViewModels);
                GruposDeClientes.Add(new ClientGroupViewModel("Sin grupo", 0, 0, sinGrupoViewModels));
            }
        }

        // Archivados: solo aparecen si coinciden con la búsqueda; no se les aplica el filtro de estado.
        var archivados = _data.Clients.Where(c => c.Archivado).ToList();
        if (FiltroGrupoId == -1)
            archivados = archivados.Where(c => CobrosHelper.GrupoDe(c, _data.Grupos) == null).ToList();
        else if (FiltroGrupoId != 0)
            archivados = archivados.Where(c => c.GrupoId == FiltroGrupoId).ToList();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            archivados = archivados.Where(c => c.Nombre.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (archivados.Count > 0)
        {
            var archivadosViewModels = archivados.Select(c => new ClientItemViewModel(c, _data.Grupos, _data.Config, mesKey)).ToList();
            SortViewModels(archivadosViewModels);
            GruposDeClientes.Add(new ClientGroupViewModel("Archivados", 0, -1, archivadosViewModels));
        }
    }

    private void SortViewModels(List<ClientItemViewModel> list)
    {
        if (OrdenSort == "nombre")
            list.Sort((a, b) => string.Compare(a.Nombre, b.Nombre, StringComparison.OrdinalIgnoreCase));
        else
            list.Sort((a, b) => b.SaldoAbs.CompareTo(a.SaldoAbs));
    }
}

public class ClientGroupViewModel : ObservableCollection<ClientItemViewModel>
{
    public string Nombre { get; }
    public string Descripcion { get; }
    public int GrupoId { get; }
    public bool IsRealGroup => GrupoId > 0;

    public ClientGroupViewModel(string nombre, double cuota, int grupoId, List<ClientItemViewModel> clients) : base(clients)
    {
        Nombre = nombre;
        GrupoId = grupoId;
        if (cuota > 0)
        {
            double total = cuota * clients.Count;
            Descripcion = $"Cuota {CobrosHelper.FormatMoney(cuota)} · {clients.Count} cliente(s) · {CobrosHelper.FormatMoney(total)}";
        }
        else
        {
            Descripcion = $"{clients.Count} cliente(s)";
        }
    }
}
