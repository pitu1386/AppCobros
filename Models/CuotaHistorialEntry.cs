using System.Text.Json.Serialization;

namespace AppCobros.Models;

public class CuotaHistorialEntry
{
    [JsonPropertyName("fecha")]
    public string Fecha { get; set; } = string.Empty; // YYYY-MM-DD

    [JsonPropertyName("cuota")]
    public double Cuota { get; set; }
}
