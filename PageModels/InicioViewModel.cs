using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppCobros.Models;
using AppCobros.Services;
using AppCobros.Utilities;

namespace AppCobros.PageModels;

public partial class InicioViewModel : BaseViewModel
{
    private readonly IDataService _dataService;
    private readonly IWhatsAppService _whatsAppService;
    private CobrosData? _data;

    [ObservableProperty]
    private string _mesActual = string.Empty;

    [ObservableProperty]
    private double _totalCobradoMes;

    [ObservableProperty]
    private double _totalPendiente;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayClientesSinFacturar))]
    private int _clientesSinFacturarCount;

    public bool HayClientesSinFacturar => ClientesSinFacturarCount > 0;
    public bool TodosFacturados => ClientesSinFacturarCount == 0;

    [ObservableProperty]
    private ObservableCollection<ClientItemViewModel> _clientesConDeuda = new();

    public InicioViewModel(IDataService dataService, IWhatsAppService whatsAppService)
    {
        _dataService = dataService;
        _whatsAppService = whatsAppService;
        Title = "Inicio";
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        _data = await _dataService.LoadDataAsync();
        
        var date = DateTime.Now;
        MesActual = CobrosHelper.MesLabel(CobrosHelper.MesKey(date));
        var mesKey = CobrosHelper.MesKey(date);

        double pendiente = 0;
        double cobradoMes = 0;

        ClientesConDeuda.Clear();

        if (_data.Clients != null)
        {
            var sinFacturar = _data.Clients.Where(c => !CobrosHelper.FacturadoMes(c, mesKey)).ToList();
            ClientesSinFacturarCount = sinFacturar.Count;

            foreach (var c in _data.Clients)
            {
                double s = CobrosHelper.SaldoDe(c);
                if (s > 0) pendiente += s;

                if (c.Movimientos != null)
                {
                    foreach (var m in c.Movimientos)
                    {
                        if (m.Tipo == "pago" && m.Fecha.StartsWith(mesKey))
                        {
                            cobradoMes += m.Monto;
                        }
                    }
                }

                if (s > 0)
                {
                    ClientesConDeuda.Add(new ClientItemViewModel(c, _data.Grupos, _data.Config, mesKey));
                }
            }

            // Ordenar según config
            var orden = _data.Config.Orden ?? "deuda";
            var list = ClientesConDeuda.ToList();
            list.Sort((a, b) => orden == "nombre" 
                ? string.Compare(a.Client.Nombre, b.Client.Nombre, StringComparison.OrdinalIgnoreCase)
                : b.SaldoAbs.CompareTo(a.SaldoAbs));

            ClientesConDeuda.Clear();
            foreach (var item in list) ClientesConDeuda.Add(item);
        }

        TotalPendiente = pendiente;
        TotalCobradoMes = cobradoMes;

        IsBusy = false;
    }

    [RelayCommand]
    private async Task CargarCuotasDelMesAsync()
    {
        if (_data == null) return;
        var mesKey = CobrosHelper.MesKey(DateTime.Now);
        var sinFacturar = _data.Clients.Where(c => !CobrosHelper.FacturadoMes(c, mesKey)).ToList();
        
        if (sinFacturar.Count == 0) return;

        double total = sinFacturar.Sum(c => CobrosHelper.CuotaDe(c, _data.Grupos, _data.Config));

        bool confirm = await Shell.Current.DisplayAlertAsync("Confirmar", 
            $"Se va a cargar la cuota de {MesActual} a {sinFacturar.Count} cliente(s) por un total de {CobrosHelper.FormatMoney(total)}. ¿Confirmás?", "Sí", "No");

        if (!confirm) return;

        foreach (var c in sinFacturar)
        {
            c.Meses.Add(mesKey);
            c.Movimientos.Add(new Movimiento
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + c.Id,
                Tipo = "cargo",
                Fecha = CobrosHelper.HoyISO(),
                Mes = mesKey,
                Concepto = $"Cuota {MesActual}",
                Monto = CobrosHelper.CuotaDe(c, _data.Grupos, _data.Config)
            });
        }

        await _dataService.SaveDataAsync(_data);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task ReclamarATodosAsync()
    {
        if (_data == null) return;
        var mesKey = CobrosHelper.MesKey(DateTime.Now);
        var clientesAReclamar = _data.Clients
            .Select(c => new { Client = c, TotalExigible = CobrosHelper.TotalDe(CobrosHelper.ExigiblesDe(c, mesKey)) })
            .Where(x => x.TotalExigible > 0)
            .ToList();

        if (clientesAReclamar.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Sin deudas", "No hay clientes con deuda exigible.", "OK");
            return;
        }

        await Shell.Current.DisplayAlertAsync("Aviso", "Esta función debe implementarse con una página o popup de envío masivo.", "OK");
        // Para simplificar, podríamos simplemente avisar que se debe hacer desde el detalle de cada cliente, 
        // o implementar la hoja de "Reclamar deudas" como en React.
    }
}

public class ClientItemViewModel
{
    public Client Client { get; }
    public string Iniciales { get; }
    public string Nombre { get; }
    public string Detalle { get; }
    public double Saldo { get; }
    public double SaldoAbs => Math.Abs(Saldo);
    public string SaldoTexto { get; }
    public string EstadoTexto { get; }
    public bool TieneDeuda => Saldo > 0;
    public bool Exigible => _exig > 0;
    public bool MesVencido => Client.MesVencido;

    private double _exig;

    public ClientItemViewModel(Client c, IEnumerable<Grupo> grupos, Config cfg, string mesActual)
    {
        Client = c;
        var g = CobrosHelper.GrupoDe(c, grupos);
        
        var words = c.Nombre.Split(' ');
        Iniciales = string.Join("", words.Take(2).Select(w => w.Length > 0 ? w[0].ToString() : "")).ToUpper();
        
        Nombre = c.Nombre;
        
        string anexoTxt = c.Anexos > 0 ? $" · {c.Anexos} anexo" + (c.Anexos > 1 ? "s" : "") : "";
        Detalle = $"{(g != null ? g.Nombre : "sin grupo")}{anexoTxt} · cuota {CobrosHelper.FormatMoney(CobrosHelper.CuotaDe(c, grupos, cfg))}";

        Saldo = CobrosHelper.SaldoDe(c);
        _exig = CobrosHelper.TotalDe(CobrosHelper.ExigiblesDe(c, mesActual));

        SaldoTexto = CobrosHelper.FormatMoney(SaldoAbs);
        
        if (Saldo > 0)
            EstadoTexto = _exig > 0 ? "debe" : "mes en curso";
        else if (Saldo < 0)
            EstadoTexto = "a favor";
        else
            EstadoTexto = "al día";
    }
}
