using System.Text.Json.Serialization;

namespace AppCobros.Models;

/// Movimiento borrado que queda guardado un tiempo para poder restaurarlo.
public class MovimientoEliminado
{
    [JsonPropertyName("clienteId")]
    public int ClienteId { get; set; }

    [JsonPropertyName("clienteNombre")]
    public string ClienteNombre { get; set; } = string.Empty;

    [JsonPropertyName("eliminadoEl")]
    public string EliminadoEl { get; set; } = string.Empty; // YYYY-MM-DD HH:mm

    /// El mes se libera al borrar un cargo mensual; hay que volver a marcarlo si se restaura.
    [JsonPropertyName("mesLiberado")]
    public string? MesLiberado { get; set; }

    [JsonPropertyName("movimiento")]
    public Movimiento Movimiento { get; set; } = new();

    [JsonIgnore]
    public string Resumen => $"{Movimiento.FechaCorta} · {Movimiento.Concepto}";

    [JsonIgnore]
    public string Detalle => $"{ClienteNombre} · eliminado el {EliminadoEl}";

    [JsonIgnore]
    public string MontoTexto => Movimiento.MontoConSigno;

    [JsonIgnore]
    public bool EsCargo => Movimiento.Tipo == "cargo";
}
