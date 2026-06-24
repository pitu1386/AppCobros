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
    
    // Ignorado al serializar, solo para ayudar en la UI con pagos parciales
    [JsonIgnore]
    public double Resto { get; set; }
}
