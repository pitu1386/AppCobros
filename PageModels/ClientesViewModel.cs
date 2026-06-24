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

    public ClientesViewModel(IDataService dataService)
    {
        _dataService = dataService;
        Title = "Clientes";
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        _data = await _dataService.LoadDataAsync();
        FilterClientes();
        IsBusy = false;
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterClientes();
    }

    private void FilterClientes()
    {
        if (_data == null) return;

        GruposDeClientes.Clear();
        var mesKey = CobrosHelper.MesKey(DateTime.Now);

        foreach (var g in _data.Grupos)
        {
            var clientes = _data.Clients.Where(c => c.GrupoId == g.Id).ToList();
            
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                clientes = clientes.Where(c => c.Nombre.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (clientes.Count > 0 || string.IsNullOrWhiteSpace(SearchText))
            {
                var viewModels = clientes.Select(c => new ClientItemViewModel(c, _data.Grupos, _data.Config, mesKey)).ToList();
                viewModels.Sort((a, b) => b.SaldoAbs.CompareTo(a.SaldoAbs));

                GruposDeClientes.Add(new ClientGroupViewModel(g.Nombre, g.Cuota, viewModels));
            }
        }

        var sinGrupo = _data.Clients.Where(c => CobrosHelper.GrupoDe(c, _data.Grupos) == null).ToList();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            sinGrupo = sinGrupo.Where(c => c.Nombre.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (sinGrupo.Count > 0)
        {
            var viewModels = sinGrupo.Select(c => new ClientItemViewModel(c, _data.Grupos, _data.Config, mesKey)).ToList();
            viewModels.Sort((a, b) => b.SaldoAbs.CompareTo(a.SaldoAbs));
            GruposDeClientes.Add(new ClientGroupViewModel("Sin grupo", 0, viewModels));
        }
    }
}

public class ClientGroupViewModel : ObservableCollection<ClientItemViewModel>
{
    public string Nombre { get; }
    public string Descripcion { get; }

    public ClientGroupViewModel(string nombre, double cuota, List<ClientItemViewModel> clients) : base(clients)
    {
        Nombre = nombre;
        Descripcion = cuota > 0 ? $"Cuota {CobrosHelper.FormatMoney(cuota)} · {clients.Count} cliente(s)" : $"{clients.Count} cliente(s)";
    }
}
