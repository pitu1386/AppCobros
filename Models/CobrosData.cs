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

    [JsonPropertyName("nextId")]
    public int NextId { get; set; } = 1;

    [JsonPropertyName("nextGid")]
    public int NextGid { get; set; } = 4;
}
