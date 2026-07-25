using System.Collections.ObjectModel;
using AppCobros.Models;

namespace AppCobros.Pages;

public record CargoManualResult(string Concepto, double Monto, DateTime Fecha, bool EsCuotaMensual);

public partial class CargoManualPage : ContentPage
{
    private const string OpcionOtro = "✎ Otro (escribir)";

    private readonly TaskCompletionSource<CargoManualResult?> _tcs = new();
    private readonly List<ConceptoCargo> _conceptos;

    // Se completa al cerrar la página: con los datos del cargo, o null si se canceló.
    public Task<CargoManualResult?> Result => _tcs.Task;

    /// Opciones del desplegable: los conceptos de Ajustes más la alternativa de escribir uno nuevo.
    public ObservableCollection<string> OpcionesConcepto { get; } = new();

    public CargoManualPage(IEnumerable<ConceptoCargo>? conceptos = null)
    {
        InitializeComponent();

        _conceptos = (conceptos ?? Enumerable.Empty<ConceptoCargo>())
            .Where(c => !string.IsNullOrWhiteSpace(c.Nombre))
            .ToList();

        foreach (var c in _conceptos)
            OpcionesConcepto.Add(c.Etiqueta);
        OpcionesConcepto.Add(OpcionOtro);

        bool hayConceptos = _conceptos.Count > 0;
        ConceptoPicker.IsVisible = hayConceptos;
        SinConceptosHint.IsVisible = !hayConceptos;
        // Sin conceptos configurados el campo libre es la única opción, así que queda visible.
        ConceptoEntry.IsVisible = !hayConceptos;

        BindingContext = this;
        FechaPicker.Date = DateTime.Today;
    }

    private void ConceptoPicker_SelectedIndexChanged(object? sender, EventArgs e)
    {
        int i = ConceptoPicker.SelectedIndex;
        if (i < 0) return;

        // La última opción es "Otro": se muestra el campo libre y no se toca nada más.
        if (i >= _conceptos.Count)
        {
            ConceptoEntry.IsVisible = true;
            ConceptoEntry.Focus();
            return;
        }

        var concepto = _conceptos[i];
        ConceptoEntry.IsVisible = false;
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
            string mensaje = _conceptos.Count > 0
                ? "Elegí un concepto de la lista o seleccioná «Otro» para escribir uno."
                : "Ingresá un concepto para el cargo.";
            await DisplayAlertAsync("Falta el concepto", mensaje, "OK");
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
