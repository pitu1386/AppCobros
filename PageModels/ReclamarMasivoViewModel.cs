using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using AppCobros.Models;
using AppCobros.Services;
using AppCobros.Utilities;

namespace AppCobros.PageModels;

public partial class ReclamarMasivoViewModel : BaseViewModel
{
    private readonly IDataService _dataService;
    private readonly IWhatsAppService _whatsAppService;
    private CobrosData? _data;

    public ObservableCollection<ReclamarItemViewModel> Clientes { get; } = new();

    public ReclamarMasivoViewModel(IDataService dataService, IWhatsAppService whatsAppService)
    {
        _dataService = dataService;
        _whatsAppService = whatsAppService;
        Title = "Reclamar deudas";
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        _data = await _dataService.LoadDataAsync();
        var mesKey = CobrosHelper.MesKey(DateTime.Now);

        Clientes.Clear();
        var conDeuda = _data.Clients
            .Where(c => !c.Archivado)
            .Select(c => new { Client = c, Total = CobrosHelper.TotalDe(CobrosHelper.ExigiblesDe(c, mesKey)) })
            .Where(x => x.Total > 0)
            .OrderByDescending(x => x.Total);

        foreach (var x in conDeuda)
            Clientes.Add(new ReclamarItemViewModel(x.Client, x.Total, _data, mesKey, _dataService, _whatsAppService));

        IsBusy = false;
    }
}

public partial class ReclamarItemViewModel : ObservableObject
{
    private readonly Client _client;
    private readonly CobrosData _data;
    private readonly string _mesKey;
    private readonly IDataService _dataService;
    private readonly IWhatsAppService _whatsAppService;

    public string Nombre => _client.Nombre;
    public string DeudaTexto => $"Debe {CobrosHelper.FormatMoney(Total)}";
    public double Total { get; }

    [ObservableProperty]
    private bool _avisadoHoy;

    public bool TieneTelefono => !string.IsNullOrWhiteSpace(_client.Telefono);
    public bool SinTelefono => !TieneTelefono;

    public ReclamarItemViewModel(Client client, double total, CobrosData data, string mesKey,
        IDataService dataService, IWhatsAppService whatsAppService)
    {
        _client = client;
        _data = data;
        _mesKey = mesKey;
        _dataService = dataService;
        _whatsAppService = whatsAppService;
        Total = total;
        AvisadoHoy = client.UltRec == CobrosHelper.HoyISO();
    }

    [RelayCommand]
    private async Task EnviarAsync()
    {
        var items = CobrosHelper.ExigiblesDe(_client, _mesKey);
        var detalle = items.Count > 0
            ? "Detalle pendiente:\n" + string.Join("\n", items.Select(p =>
                $"• {p.Concepto}: {CobrosHelper.FormatMoney(p.Resto)}{(p.Resto < p.Monto ? " (resto)" : "")}"))
            : "No te quedan cuotas pendientes ✅";
        var saldoTxt = Total > 0 ? CobrosHelper.FormatMoney(Total) : "$ 0 (cuenta al día ✅)";
        var mensaje = (_data.Config.PlantillaRec ?? string.Empty)
            .Replace("{nombre}", _client.Nombre)
            .Replace("{detalle}", detalle)
            .Replace("{saldo}", saldoTxt)
            .Replace("{enlace_pago}", _data.Config.EnlacePago ?? string.Empty);

        await _whatsAppService.SendMessageAsync(_client.Telefono, mensaje);

        _client.UltRec = CobrosHelper.HoyISO();
        await _dataService.SaveDataAsync(_data);
        AvisadoHoy = true;
    }
}
