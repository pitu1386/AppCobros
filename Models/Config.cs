using System.Text.Json.Serialization;

namespace AppCobros.Models;

public class Config
{
    [JsonPropertyName("anexo")]
    public double Anexo { get; set; } = 8000;

    [JsonPropertyName("plantilla")]
    public string Plantilla { get; set; } = "Hola {nombre}! Te confirmo que registramos tu pago de {pago} con fecha {fecha}.\n\n{detalle}\n\nSaldo: {saldo}. ¡Muchas gracias!";

    [JsonPropertyName("plantillaRec")]
    public string PlantillaRec { get; set; } = "Hola {nombre}! 👋 ¿Cómo andás? Te paso el resumen de tu cuenta del sistema 🧾\n\n{detalle}\n\n💰 *Total: {saldo}*\n\nCuando puedas coordinamos el pago. ¡Cualquier cosa escribime, gracias! 🙌";

    [JsonPropertyName("enlacePago")]
    public string EnlacePago { get; set; } = string.Empty;

    [JsonPropertyName("orden")]
    public string Orden { get; set; } = "deuda";

    [JsonPropertyName("cotizacionEuro")]
    public double CotizacionEuro { get; set; } = 0; // Pesos por 1 euro. 0 = no configurada, oculta la card.
}
