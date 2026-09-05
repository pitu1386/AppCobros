using System.Text.Json.Serialization;
using LibretaCobros.Utilities;

namespace LibretaCobros.Models;

// Los [JsonPropertyName] usan snake_case: coinciden con las columnas de Postgres,
// así PostgREST serializa y deserializa directo sin capa de DTOs.
// Todo lo que NO es columna va con [JsonIgnore] o PostgREST rechaza el upsert.

public class Grupo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("cuota")] public double Cuota { get; set; }
    [JsonPropertyName("historial_cuota")] public List<CuotaHistorialEntry> HistorialCuota { get; set; } = new();
}

public class CuotaHistorialEntry
{
    [JsonPropertyName("fecha")] public string Fecha { get; set; } = "";   // YYYY-MM-DD
    [JsonPropertyName("cuota")] public double Cuota { get; set; }
}

public class Cliente
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("telefono")] public string Telefono { get; set; } = "";
    [JsonPropertyName("grupo_id")] public string GrupoId { get; set; } = "";
    [JsonPropertyName("anexos")] public int Anexos { get; set; }
    [JsonPropertyName("mes_vencido")] public bool MesVencido { get; set; }
    [JsonPropertyName("archivado")] public bool Archivado { get; set; }
    [JsonPropertyName("meses")] public List<string> Meses { get; set; } = new();   // "YYYY-MM" facturados
    [JsonPropertyName("ult_rec")] public string? UltRec { get; set; }              // YYYY-MM-DD del último reclamo

    /// <summary>Movimientos del cliente. No es columna: se arma en memoria desde la tabla movimientos.</summary>
    [JsonIgnore] public List<Movimiento> Movimientos { get; set; } = new();
}

public class Movimiento
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("cliente_id")] public string ClienteId { get; set; } = "";
    [JsonPropertyName("tipo")] public string Tipo { get; set; } = "";   // "cargo" | "pago"
    [JsonPropertyName("fecha")] public string Fecha { get; set; } = ""; // YYYY-MM-DD
    [JsonPropertyName("mes")] public string? Mes { get; set; }          // YYYY-MM (solo cargos mensuales)
    [JsonPropertyName("concepto")] public string Concepto { get; set; } = "";
    [JsonPropertyName("monto")] public double Monto { get; set; }
    [JsonPropertyName("cotizacion_euro")] public double? CotizacionEuro { get; set; }

    [JsonIgnore] public double Resto { get; set; }   // ayuda de UI para pagos parciales

    [JsonIgnore]
    public string FechaCorta =>
        string.IsNullOrEmpty(Fecha) ? "" :
        DateTime.TryParseExact(Fecha, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yy") : Fecha;

    [JsonIgnore]
    public string MontoConSigno =>
        Tipo == "pago" ? $"− {CobrosHelper.FormatMoney(Monto)}" : $"+ {CobrosHelper.FormatMoney(Monto)}";
}

/// <summary>Cargo frecuente precargado desde Ajustes. Se guarda dentro de config.conceptos_cargo (jsonb).</summary>
public class ConceptoCargo
{
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("monto")] public double Monto { get; set; }
    [JsonPropertyName("es_cuota_mensual")] public bool EsCuotaMensual { get; set; }

    [JsonIgnore]
    public string Etiqueta => Monto > 0 ? $"{Nombre} · {CobrosHelper.FormatMoney(Monto)}" : Nombre;
}

/// <summary>Movimiento borrado que queda un tiempo para poder restaurarlo (tabla papelera).</summary>
public class MovimientoEliminado
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";              // = id del movimiento
    [JsonPropertyName("cliente_id")] public string ClienteId { get; set; } = "";
    [JsonPropertyName("cliente_nombre")] public string ClienteNombre { get; set; } = "";
    [JsonPropertyName("eliminado_el")] public string EliminadoEl { get; set; } = ""; // YYYY-MM-DD HH:mm
    [JsonPropertyName("mes_liberado")] public string? MesLiberado { get; set; }
    [JsonPropertyName("movimiento")] public Movimiento Movimiento { get; set; } = new();

    [JsonIgnore] public string Resumen => $"{Movimiento.FechaCorta} · {Movimiento.Concepto}";
    [JsonIgnore] public string Detalle => $"{ClienteNombre} · eliminado el {EliminadoEl}";
    [JsonIgnore] public string MontoTexto => Movimiento.MontoConSigno;
    [JsonIgnore] public bool EsCargo => Movimiento.Tipo == "cargo";
}

public class Config
{
    [JsonPropertyName("id")] public string Id { get; set; } = "current";
    [JsonPropertyName("anexo")] public double Anexo { get; set; } = 8000;
    [JsonPropertyName("plantilla")] public string Plantilla { get; set; } =
        "Hola {nombre}! Te confirmo que registramos tu pago de {pago} con fecha {fecha}.\n\n{detalle}\n\nSaldo: {saldo}. ¡Muchas gracias!";
    [JsonPropertyName("plantilla_rec")] public string PlantillaRec { get; set; } =
        "Hola {nombre}! 👋 ¿Cómo andás? Te paso el resumen de tu cuenta 🧾\n\n{detalle}\n\n💰 *Total: {saldo}*\n\nCuando puedas coordinamos el pago. ¡Gracias! 🙌";
    [JsonPropertyName("enlace_pago")] public string EnlacePago { get; set; } = "";
    [JsonPropertyName("orden")] public string Orden { get; set; } = "deuda";
    [JsonPropertyName("cotizacion_euro")] public double CotizacionEuro { get; set; }
    [JsonPropertyName("conceptos_cargo")] public List<ConceptoCargo> ConceptosCargo { get; set; } = new();
}

/// <summary>Estado completo en memoria (y caché en localStorage para pintar al instante).</summary>
public class LibretaData
{
    public Config Config { get; set; } = new();
    public List<Grupo> Grupos { get; set; } = new();
    public List<Cliente> Clientes { get; set; } = new();
    public List<MovimientoEliminado> Papelera { get; set; } = new();
}
