using System.Text.Json.Serialization;

namespace AppCobros.Models;

public class Grupo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("cuota")]
    public double Cuota { get; set; }
}
