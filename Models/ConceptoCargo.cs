using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AppCobros.Models;

/// Cargo frecuente precargado desde Ajustes para no tener que tipear concepto y monto cada vez.
public partial class ConceptoCargo : ObservableObject
{
    [ObservableProperty]
    [property: JsonPropertyName("nombre")]
    private string _nombre = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("monto")]
    private double _monto;

    /// Marca el mes como facturado al usarlo, igual que una cuota mensual.
    [ObservableProperty]
    [property: JsonPropertyName("esCuotaMensual")]
    private bool _esCuotaMensual;

    [JsonIgnore]
    public string Etiqueta => Monto > 0
        ? $"{Nombre} · {Utilities.CobrosHelper.FormatMoney(Monto)}"
        : Nombre;
}
