using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AppCobros.Models;
using AppCobros.Services;
using AppCobros.Utilities;

namespace AppCobros.PageModels;

[QueryProperty(nameof(ClientId), "ClientId")]
public partial class ClienteDetalleViewModel : BaseViewModel
{
    private readonly IDataService _dataService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IReceiptService _receiptService;
    private CobrosData? _data;

    [ObservableProperty]
    private int _clientId;

    [ObservableProperty]
    private Client? _client;

    [ObservableProperty]
    private double _saldo;

    [ObservableProperty]
    private string _estadoTexto = string.Empty;

    [ObservableProperty]
    private string _detalleTexto = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExigible))]
    private double _totalExigible;

    public bool IsExigible => TotalExigible > 0;

    [ObservableProperty]
    private ObservableCollection<Movimiento> _movimientos = new();

    [ObservableProperty]
    private ObservableCollection<Movimiento> _pendientes = new();

    public ClienteDetalleViewModel(IDataService dataService, IWhatsAppService whatsAppService, IReceiptService receiptService)
    {
        _dataService = dataService;
        _whatsAppService = whatsAppService;
        _receiptService = receiptService;
    }

    partial void OnClientIdChanged(int value)
    {
        _ = LoadDataAsync();
    }

    public async Task LoadDataAsync()
    {
        IsBusy = true;
        _data = await _dataService.LoadDataAsync();
        Client = _data.Clients.FirstOrDefault(c => c.Id == ClientId);
        
        if (Client != null)
        {
            Title = Client.Nombre;
            var mesActual = CobrosHelper.MesKey(DateTime.Now);
            var g = CobrosHelper.GrupoDe(Client, _data.Grupos);

            Saldo = CobrosHelper.SaldoDe(Client);
            var exig = CobrosHelper.ExigiblesDe(Client, mesActual);
            TotalExigible = CobrosHelper.TotalDe(exig);

            if (TotalExigible > 0) EstadoTexto = "Debe";
            else if (Saldo > 0) EstadoTexto = "Mes en curso";
            else if (Saldo < 0) EstadoTexto = "A favor";
            else EstadoTexto = "Al día";

            string anexoTxt = Client.Anexos > 0 ? $" (incluye {Client.Anexos} anexo" + (Client.Anexos > 1 ? "s)" : ")") : "";
            DetalleTexto = $"{(g != null ? g.Nombre : "Sin grupo")} · cuota {CobrosHelper.FormatMoney(CobrosHelper.CuotaDe(Client, _data.Grupos, _data.Config))}{anexoTxt}";
            if (Client.MesVencido) DetalleTexto += " · paga a mes vencido";
            if (!string.IsNullOrEmpty(Client.Telefono)) DetalleTexto += $" · 📱 {Client.Telefono}";

            Pendientes = new ObservableCollection<Movimiento>(CobrosHelper.PendientesDe(Client));
            
            var movs = Client.Movimientos.OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id).ToList();
            Movimientos = new ObservableCollection<Movimiento>(movs);
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RegistrarCobroAsync()
    {
        if (Client == null || _data == null) return;
        
        string result = await Shell.Current.DisplayPromptAsync("Registrar cobro", $"Monto sugerido: {CobrosHelper.FormatMoney(TotalExigible > 0 ? TotalExigible : Math.Max(Saldo, 0))}", keyboard: Keyboard.Numeric);
        
        if (double.TryParse(result, out double monto) && monto > 0)
        {
            var pago = new Movimiento
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Tipo = "pago",
                Fecha = CobrosHelper.HoyISO(),
                Concepto = "Pago recibido",
                Monto = monto
            };
            
            Client.Movimientos.Add(pago);
            await _dataService.SaveDataAsync(_data);
            await LoadDataAsync();

            bool sendReceipt = await Shell.Current.DisplayAlertAsync("Recibo Generado", "¿Querés enviarle el comprobante de pago por WhatsApp al cliente?", "Sí, enviar", "No, solo registrar");
            if (sendReceipt)
            {
                IsBusy = true;
                try
                {
                    string receiptPath = await _receiptService.GenerateReceiptAsync(Client, pago);
                    await Share.RequestAsync(new ShareFileRequest
                    {
                        Title = "Recibo de Pago",
                        File = new ShareFile(receiptPath)
                    });
                }
                catch (Exception)
                {
                    await Shell.Current.DisplayAlertAsync("Error", "No se pudo generar el recibo.", "OK");
                }
                finally
                {
                    IsBusy = false;
                }
            }
            else
            {
                string msg = _data.Config.Plantilla
                    .Replace("{nombre}", Client.Nombre)
                    .Replace("{pago}", CobrosHelper.FormatMoney(monto))
                    .Replace("{fecha}", DateTime.Now.ToString("dd/MM/yy"))
                    .Replace("{saldo}", CobrosHelper.FormatMoney(Math.Max(0, Saldo - monto)))
                    .Replace("{detalle}", "Gracias por su pago.");

                await _whatsAppService.SendMessageAsync(Client.Telefono, msg);
            }
        }
    }

    [RelayCommand]
    private async Task EnviarRecordatorioAsync()
    {
        if (Client == null || _data == null) return;

        var items = CobrosHelper.ExigiblesDe(Client, CobrosHelper.MesKey(DateTime.Now));
        string detalle = items.Count > 0 
            ? "Detalle pendiente:\n" + string.Join("\n", items.Select(p => $"• {p.Concepto}: {CobrosHelper.FormatMoney(p.Resto)}"))
            : "No te quedan cuotas pendientes ✅";

        string saldoTxt = TotalExigible > 0 ? CobrosHelper.FormatMoney(TotalExigible) : "$ 0 (cuenta al día ✅)";

        string msg = _data.Config.PlantillaRec
            .Replace("{nombre}", Client.Nombre)
            .Replace("{detalle}", detalle)
            .Replace("{saldo}", saldoTxt)
            .Replace("{enlace_pago}", _data.Config.EnlacePago ?? "");

        await _whatsAppService.SendMessageAsync(Client.Telefono, msg);
        
        Client.UltRec = CobrosHelper.HoyISO();
        await _dataService.SaveDataAsync(_data);
    }

    [RelayCommand]
    private async Task AgregarCargoAsync()
    {
        if (Client == null || _data == null) return;

        string concepto = await Shell.Current.DisplayPromptAsync("Cargo manual", "Concepto (ej: Ajuste, Instalación)");
        if (string.IsNullOrWhiteSpace(concepto)) return;

        string montoStr = await Shell.Current.DisplayPromptAsync("Monto", "Ingrese el monto del cargo", keyboard: Keyboard.Numeric);
        if (double.TryParse(montoStr, out double monto) && monto > 0)
        {
            Client.Movimientos.Add(new Movimiento
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Tipo = "cargo",
                Fecha = CobrosHelper.HoyISO(),
                Concepto = concepto,
                Monto = monto
            });
            await _dataService.SaveDataAsync(_data);
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task BorrarMovimientoAsync(Movimiento m)
    {
        if (Client == null || _data == null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync("Borrar", "¿Eliminar este movimiento? Si es una cuota, el mes quedará liberado.", "Sí", "No");
        if (confirm)
        {
            Client.Movimientos.Remove(Client.Movimientos.First(x => x.Id == m.Id));
            if (m.Tipo == "cargo" && !string.IsNullOrEmpty(m.Mes))
            {
                Client.Meses.Remove(m.Mes);
            }
            await _dataService.SaveDataAsync(_data);
            await LoadDataAsync();
        }
    }
}
