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
    [NotifyPropertyChangedFor(nameof(ClienteActivo))]
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
    [NotifyPropertyChangedFor(nameof(TienePendientes))]
    private ObservableCollection<Movimiento> _pendientes = new();

    public bool TienePendientes => Pendientes.Count > 0;

    public bool ClienteActivo => Client != null && !Client.Archivado;

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
        
        var pendientes = CobrosHelper.PendientesDe(Client);
        var page = new RegistrarCobroPage(TotalExigible > 0 ? TotalExigible : Math.Max(Saldo, 0), pendientes);
        await Shell.Current.Navigation.PushModalAsync(page);
        var result = await page.Result;

        if (result != null)
        {
            double monto = result.Monto;
            var pago = new Movimiento
            {
                Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Tipo = "pago",
                Fecha = result.Fecha.ToString("yyyy-MM-dd"),
                Concepto = string.IsNullOrEmpty(result.Concepto) ? "Pago recibido" : $"Pago recibido — {result.Concepto}",
                Monto = monto,
                CotizacionEuro = _data.Config.CotizacionEuro > 0 ? _data.Config.CotizacionEuro : null
            };
            
            Client.Movimientos.Add(pago);
            await _dataService.SaveDataAsync(_data);
            await LoadDataAsync();

            const string opEnviarRecibo = "📄 Enviar recibo (imagen)";
            const string opEnviarMensaje = "💬 Enviar mensaje de WhatsApp";
            const string opNoEnviar = "No enviar nada";

            string opcion = await Shell.Current.DisplayActionSheetAsync(
                "Pago registrado. ¿Avisamos al cliente?", opNoEnviar, null, opEnviarRecibo, opEnviarMensaje);

            if (opcion == opEnviarRecibo)
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
            else if (opcion == opEnviarMensaje)
            {
                string msg = _data.Config.Plantilla
                    .Replace("{nombre}", Client.Nombre)
                    .Replace("{pago}", CobrosHelper.FormatMoney(monto))
                    .Replace("{fecha}", result.Fecha.ToString("dd/MM/yy"))
                    .Replace("{saldo}", CobrosHelper.FormatMoney(Math.Max(0, Saldo - monto)))
                    .Replace("{detalle}", "Gracias por su pago.");

                await _whatsAppService.SendMessageAsync(Client.Telefono, msg);
            }
            // Cualquier otra opción (incluido "No enviar nada" o cerrar el diálogo) no envía nada.
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

        var page = new CargoManualPage(_data.Config.ConceptosCargo);
        await Shell.Current.Navigation.PushModalAsync(page);
        var result = await page.Result;
        if (result == null) return;

        string? mesKey = result.EsCuotaMensual ? CobrosHelper.MesKey(result.Fecha) : null;

        Client.Movimientos.Add(new Movimiento
        {
            Id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Tipo = "cargo",
            Fecha = result.Fecha.ToString("yyyy-MM-dd"),
            Mes = mesKey,
            Concepto = result.Concepto,
            Monto = result.Monto
        });

        if (mesKey != null && !Client.Meses.Contains(mesKey))
        {
            Client.Meses.Add(mesKey);
        }

        await _dataService.SaveDataAsync(_data);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task EnviarEstadoCuentaAsync()
    {
        if (Client == null || _data == null) return;

        IsBusy = true;
        try
        {
            string path = await _receiptService.GenerateAccountStatementAsync(Client, _data.Grupos, _data.Config);
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = $"Estado de cuenta · {Client.Nombre}",
                File = new ShareFile(path)
            });
        }
        catch (Exception)
        {
            await Shell.Current.DisplayAlertAsync("Error", "No se pudo generar el estado de cuenta.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditarClienteAsync()
    {
        if (Client == null) return;
        await Shell.Current.GoToAsync($"{nameof(ClienteFormPage)}?ClientId={Client.Id}");
    }

    [RelayCommand]
    private async Task ArchivarClienteAsync()
    {
        if (Client == null || _data == null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Archivar cliente",
            "El cliente se va a ocultar de las listas activas y de los reclamos, pero se conserva todo su historial de pagos y cargos. Pod\u00e9s reactivarlo cuando quieras desde el grupo \u00abArchivados\u00bb.",
            "Archivar", "Cancelar");
        if (!confirm) return;

        Client.Archivado = true;
        await _dataService.SaveDataAsync(_data);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task ReactivarClienteAsync()
    {
        if (Client == null || _data == null) return;

        Client.Archivado = false;
        await _dataService.SaveDataAsync(_data);
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task BorrarClienteAsync()
    {
        if (Client == null || _data == null) return;

        if (!Client.Archivado)
        {
            await Shell.Current.DisplayAlertAsync("Archiv\u00e1 primero", "Para eliminar un cliente definitivamente primero ten\u00e9s que archivarlo. As\u00ed evitamos borrar historial por error.", "OK");
            return;
        }

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Eliminar definitivamente",
            "\u00bfEliminar este cliente y todo su historial para siempre? Esta acci\u00f3n no se puede deshacer.",
            "Eliminar", "Cancelar");
        if (!confirm) return;

        _data.Clients.Remove(Client);
        await _dataService.SaveDataAsync(_data);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task BorrarMovimientoAsync(Movimiento m)
    {
        if (Client == null || _data == null) return;

        bool confirm = await Shell.Current.DisplayAlertAsync(
            "Borrar",
            $"¿Eliminar este movimiento? Va a la papelera y podés restaurarlo durante {CobrosHelper.DiasRetencionPapelera} días desde Ajustes.",
            "Sí", "No");
        if (!confirm) return;

        var original = Client.Movimientos.FirstOrDefault(x => x.Id == m.Id);
        if (original == null) return;

        var eliminado = CobrosHelper.EnviarAPapelera(_data, Client, original);
        await _dataService.SaveDataAsync(_data);
        await LoadDataAsync();

        await AppShell.DisplayUndoSnackbarAsync(
            $"Movimiento eliminado · {CobrosHelper.FormatMoney(original.Monto)}",
            () => RestaurarMovimientoAsync(eliminado));
    }

    private async Task RestaurarMovimientoAsync(MovimientoEliminado eliminado)
    {
        if (_data == null) return;

        if (!CobrosHelper.RestaurarDePapelera(_data, eliminado))
        {
            await Shell.Current.DisplayAlertAsync("No se pudo restaurar", "El cliente de ese movimiento ya no existe.", "OK");
            return;
        }

        await _dataService.SaveDataAsync(_data);
        await LoadDataAsync();
    }
}
