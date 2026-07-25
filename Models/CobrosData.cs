using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace AppCobros.Models;

public class CobrosData
{
    [JsonPropertyName("config")]
    public Config Config { get; set; } = new Config();

    [JsonPropertyName("grupos")]
    public ObservableCollection<Grupo> Grupos { get; set; } = new ObservableCollection<Grupo>();

    [JsonPropertyName("clients")]
    public ObservableCollection<Client> Clients { get; set; } = new ObservableCollection<Client>();

    /// Movimientos borrados que todavía se pueden restaurar.
    [JsonPropertyName("papelera")]
    public ObservableCollection<MovimientoEliminado> Papelera { get; set; } = new ObservableCollection<MovimientoEliminado>();

    [JsonPropertyName("nextId")]
    public int NextId { get; set; } = 1;

    [JsonPropertyName("nextGid")]
    public int NextGid { get; set; } = 4;
}
