using AppCobros.Models;
using AppCobros.Utilities;

namespace AppCobros.Pages;

public record RegistrarCobroResult(double Monto, DateTime Fecha, string? Concepto);

public partial class RegistrarCobroPage : ContentPage
{
    private readonly TaskCompletionSource<RegistrarCobroResult?> _tcs = new();
    private readonly List<Movimiento> _pendientes;
    private readonly double _montoSugeridoGeneral;

    // Se completa al cerrar la página: con los datos del cobro, o null si se canceló.
    public Task<RegistrarCobroResult?> Result => _tcs.Task;

    public RegistrarCobroPage(double montoSugerido, List<Movimiento>? pendientes = null)
    {
        InitializeComponent();
        FechaPicker.Date = DateTime.Today;
        _montoSugeridoGeneral = montoSugerido;
        _pendientes = pendientes ?? new List<Movimiento>();

        if (_pendientes.Count > 0)
        {
            CorrespondeALabel.IsVisible = true;
            PendientePicker.IsVisible = true;
            PendientePicker.Items.Add("Pago general / a cuenta");
            foreach (var p in _pendientes)
                PendientePicker.Items.Add($"{p.Concepto} — {CobrosHelper.FormatMoney(p.Resto)}");
            PendientePicker.SelectedIndex = 0;
        }

        ActualizarSugerido(montoSugerido);
    }

    private void PendientePicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        int idx = PendientePicker.SelectedIndex;
        if (idx <= 0)
        {
            ActualizarSugerido(_montoSugeridoGeneral);
        }
        else
        {
            var pendiente = _pendientes[idx - 1];
            ActualizarSugerido(pendiente.Resto);
        }
    }

    private void ActualizarSugerido(double monto)
    {
        if (monto > 0)
        {
            SugeridoLabel.Text = $"Monto sugerido: {CobrosHelper.FormatMoney(monto)}";
            MontoEntry.Text = Math.Round(monto).ToString();
        }
        else
        {
            SugeridoLabel.Text = string.Empty;
            MontoEntry.Text = string.Empty;
        }
    }

    private async void Guardar_Clicked(object? sender, EventArgs e)
    {
        if (!double.TryParse(MontoEntry.Text, out double monto) || monto <= 0)
        {
            await DisplayAlertAsync("Monto inválido", "Ingresá un monto mayor a cero.", "OK");
            return;
        }

        string? concepto = null;
        if (PendientePicker.IsVisible && PendientePicker.SelectedIndex > 0)
        {
            concepto = _pendientes[PendientePicker.SelectedIndex - 1].Concepto;
        }

        _tcs.TrySetResult(new RegistrarCobroResult(monto, FechaPicker.Date ?? DateTime.Today, concepto));
        await Navigation.PopModalAsync();
    }

    private async void Cancelar_Clicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Cubre el cierre con el botón "atrás" del sistema
        _tcs.TrySetResult(null);
    }
}
