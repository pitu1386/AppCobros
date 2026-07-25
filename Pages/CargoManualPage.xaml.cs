using System.Collections.ObjectModel;
using AppCobros.Models;

namespace AppCobros.Pages;

public record CargoManualResult(string Concepto, double Monto, DateTime Fecha, bool EsCuotaMensual);

public partial class CargoManualPage : ContentPage
{
    private readonly TaskCompletionSource<CargoManualResult?> _tcs = new();

    // Se completa al cerrar la página: con los datos del cargo, o null si se canceló.
    public Task<CargoManualResult?> Result => _tcs.Task;

    /// Cargos frecuentes configurados en Ajustes.
    public ObservableCollection<ConceptoCargo> ConceptosRapidos { get; }

    public CargoManualPage(IEnumerable<ConceptoCargo>? conceptos = null)
    {
        InitializeComponent();

        ConceptosRapidos = new ObservableCollection<ConceptoCargo>(conceptos ?? Enumerable.Empty<ConceptoCargo>());
        ConceptosCard.IsVisible = ConceptosRapidos.Count > 0;

        BindingContext = this;
        FechaPicker.Date = DateTime.Today;
    }

    private void Concepto_Tapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not ConceptoCargo concepto) return;

        ConceptoEntry.Text = concepto.Nombre;
        if (concepto.Monto > 0)
            MontoEntry.Text = concepto.Monto.ToString("0.##");
        EsCuotaSwitch.IsToggled = concepto.EsCuotaMensual;
    }

    private async void Guardar_Clicked(object? sender, EventArgs e)
    {
        string concepto = ConceptoEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(concepto))
        {
            await DisplayAlertAsync("Falta el concepto", "Ingresá un concepto para el cargo.", "OK");
            return;
        }

        if (!double.TryParse(MontoEntry.Text, out double monto) || monto <= 0)
        {
            await DisplayAlertAsync("Monto inválido", "Ingresá un monto mayor a cero.", "OK");
            return;
        }

        _tcs.TrySetResult(new CargoManualResult(concepto, monto, FechaPicker.Date ?? DateTime.Today, EsCuotaSwitch.IsToggled));
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
