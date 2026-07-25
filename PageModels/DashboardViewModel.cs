using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AppCobros.Models;
using AppCobros.Services;
using AppCobros.Utilities;
using CommunityToolkit.Mvvm.Input;

namespace AppCobros.PageModels;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly IDataService _dataService;
    private readonly IWhatsAppService _whatsAppService;
    private CobrosData? _data;

    [ObservableProperty]
    private double _cobranzaMesActual;

    [ObservableProperty]
    private double _deudaTotal;

    [ObservableProperty]
    private double _proyeccionIngresos;

    [ObservableProperty]
    private int _clientesActivos;

    [ObservableProperty]
    private string _mesActualLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayCotizacion))]
    private double _cotizacionEuro;

    [ObservableProperty]
    private string _cobradoEurosTexto = string.Empty;

    [ObservableProperty]
    private string _cotizacionDetalle = string.Empty;

    public bool HayCotizacion => CotizacionEuro > 0;

    public ObservableCollection<ClientItemViewModel> TopDeudores { get; private set; } = new();
    public ObservableCollection<HistorialMesViewModel> HistorialCobros { get; private set; } = new();


    public DashboardViewModel(IDataService dataService, IWhatsAppService whatsAppService)
    {
        _dataService = dataService;
        _whatsAppService = whatsAppService;
        Title = "Dashboard";
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        _data = await _dataService.LoadDataAsync();

        var aviso = _dataService.TomarAvisoDatosDanados();
        if (aviso != null)
        {
            await Shell.Current.DisplayAlertAsync("Atención", aviso, "OK");
        }

        var date = DateTime.Now;
        var mesKey = CobrosHelper.MesKey(date);
        MesActualLabel = CobrosHelper.MesLabel(mesKey);

        double cobradoMes = 0;
        double deudaTotal = 0;
        double proyeccion = 0;
        double eurosMes = 0;
        double cotizActual = _data.Config.CotizacionEuro;

        TopDeudores.Clear();
        HistorialCobros.Clear();

        if (_data.Clients != null)
        {
            var activos = _data.Clients.Where(c => !c.Archivado).ToList();
            ClientesActivos = activos.Count;
            var listDeudores = new List<ClientItemViewModel>();

            foreach (var c in activos)
            {
                proyeccion += CobrosHelper.CuotaDe(c, _data.Grupos, _data.Config);

                double exigible = CobrosHelper.TotalDe(CobrosHelper.ExigiblesDe(c, mesKey));
                deudaTotal += exigible;

                if (exigible > 0)
                {
                    listDeudores.Add(new ClientItemViewModel(c, _data.Grupos, _data.Config, mesKey));
                }
            }

            // El dinero cobrado es histórico: se cuenta aunque el cliente ya esté archivado.
            foreach (var c in _data.Clients)
            {
                if (c.Movimientos == null) continue;
                foreach (var m in c.Movimientos)
                {
                    if (m.Tipo == "pago" && m.Fecha.StartsWith(mesKey))
                    {
                        cobradoMes += m.Monto;

                        // Cada pago se convierte con la cotización vigente cuando se registró;
                        // los pagos viejos sin cotización guardada usan la actual.
                        double tasa = m.CotizacionEuro ?? cotizActual;
                        if (tasa > 0) eurosMes += m.Monto / tasa;
                    }
                }
            }

            listDeudores.Sort((a, b) => b.SaldoAbs.CompareTo(a.SaldoAbs));
            foreach (var deudor in listDeudores.Take(3))
            {
                TopDeudores.Add(deudor);
            }

            // Calcular últimos 6 meses
            for (int i = 5; i >= 0; i--)
            {
                var dt = date.AddMonths(-i);
                var mk = CobrosHelper.MesKey(dt);
                string label = CobrosHelper.MesLabel(mk);

                double totalMes = 0;
                foreach (var c in _data.Clients)
                {
                    if (c.Movimientos != null)
                    {
                        totalMes += c.Movimientos
                          .Where(m => m.Tipo == "pago" && m.Fecha.StartsWith(mk))
                          .Sum(m => m.Monto);
                    }
                }
                HistorialCobros.Add(new HistorialMesViewModel(label, totalMes));
            }

            // Normalize heights for UI simple bars
            double maxMonto = HistorialCobros.Count > 0 ? HistorialCobros.Max(h => h.Monto) : 0;
            if (maxMonto > 0)
            {
                foreach (var h in HistorialCobros)
                {
                    h.HeightPercentage = (h.Monto / maxMonto) * 100;
                    h.HeightPercentage = Math.Max(h.HeightPercentage, 5); // At least 5% so it's visible
                }
            }
        }

        CobranzaMesActual = cobradoMes;
        DeudaTotal = deudaTotal;
        ProyeccionIngresos = proyeccion;

        CotizacionEuro = cotizActual;
        if (CotizacionEuro > 0)
        {
            var culture = new System.Globalization.CultureInfo("es-AR");
            CobradoEurosTexto = "€ " + eurosMes.ToString("N2", culture);
            CotizacionDetalle = $"Cotización actual: $ {CotizacionEuro.ToString("N0", culture)} por € (cada pago usa la tasa de su fecha)";
        }

        IsBusy = false;
    }

    [RelayCommand]
    private async Task EnviarRecordatorioAsync(ClientItemViewModel vm)
    {
        if (vm == null || _data == null) return;

        string detalle = vm.Exigible
            ? "Tenes una cuota pendiente de pago, por favor comunicate a la brevedad."
            : "No te quedan cuotas pendientes ✅";

        string msg = _data.Config.PlantillaRec
            .Replace("{nombre}", vm.Nombre)
            .Replace("{detalle}", detalle)
            .Replace("{saldo}", vm.SaldoTexto)
            .Replace("{enlace_pago}", _data.Config.EnlacePago ?? "");

        await _whatsAppService.SendMessageAsync(vm.Client.Telefono, msg);
    }
}

public class HistorialMesViewModel
{
    public string MesLabel { get; }
    public double Monto { get; }
    public string MontoTexto => "$ " + Math.Round(Monto).ToString("N0", new System.Globalization.CultureInfo("es-AR"));
    public double HeightPercentage { get; set; } = 0;

    public HistorialMesViewModel(string mesLabel, double monto)
    {
        MesLabel = mesLabel;
        Monto = (double)monto;
    }
}
