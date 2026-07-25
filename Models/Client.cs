using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace AppCobros.Models;

public class Client
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("telefono")]
    public string Telefono { get; set; } = string.Empty;

    [JsonPropertyName("grupoId")]
    public int GrupoId { get; set; }

    [JsonPropertyName("anexos")]
    public int Anexos { get; set; }

    [JsonPropertyName("mesVencido")]
    public bool MesVencido { get; set; }

    // Cliente dado de baja: se oculta de las listas activas pero conserva todo su historial
    [JsonPropertyName("archivado")]
    public bool Archivado { get; set; }

    [JsonPropertyName("movimientos")]
    public ObservableCollection<Movimiento> Movimientos { get; set; } = new ObservableCollection<Movimiento>();

    [JsonPropertyName("meses")]
    public ObservableCollection<string> Meses { get; set; } = new ObservableCollection<string>();

    [JsonPropertyName("ultRec")]
    public string? UltRec { get; set; }
}
