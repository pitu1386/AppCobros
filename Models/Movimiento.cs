using System.Text.Json.Serialization;

namespace AppCobros.Models;

public class Movimiento
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = string.Empty; // "cargo" o "pago"

    [JsonPropertyName("fecha")]
    public string Fecha { get; set; } = string.Empty; // YYYY-MM-DD

    [JsonPropertyName("mes")]
    public string? Mes { get; set; } // YYYY-MM (solo para cargos mensuales)

    [JsonPropertyName("concepto")]
    public string Concepto { get; set; } = string.Empty;

    [JsonPropertyName("monto")]
    public double Monto { get; set; }

    // Cotización del euro vigente al registrar el pago; permite calcular euros con la tasa real de cada cobro
    [JsonPropertyName("cotizacionEuro")]
    public double? CotizacionEuro { get; set; }
    
    // Ignorado al serializar, solo para ayudar en la UI con pagos parciales
    [JsonIgnore]
    public double Resto { get; set; }

    [JsonIgnore]
    public string FechaCorta =>
        string.IsNullOrEmpty(Fecha) ? "" :
        DateTime.TryParseExact(Fecha, "yyyy-MM-dd", null,
            System.Globalization.DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yy") : Fecha;

    [JsonIgnore]
    public string MontoConSigno =>
        Tipo == "pago"
            ? $"− {Utilities.CobrosHelper.FormatMoney(Monto)}"
            : $"+ {Utilities.CobrosHelper.FormatMoney(Monto)}";
}
