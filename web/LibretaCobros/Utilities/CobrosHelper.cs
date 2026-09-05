using System.Globalization;
using LibretaCobros.Models;

namespace LibretaCobros.Utilities;

public static class CobrosHelper
{
    public static readonly string[] MESES =
        { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    private static readonly CultureInfo EsAr = CultureInfo.GetCultureInfo("es-AR");

    public static string FormatMoney(double amount) => "$ " + Math.Round(amount).ToString("N0", EsAr);

    public static string HoyISO() => DateTime.Now.ToString("yyyy-MM-dd");

    public static string MesKey(DateTime date) => date.ToString("yyyy-MM");

    public static string MesLabel(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        var parts = key.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[1], out int month) && month is >= 1 and <= 12)
            return $"{MESES[month - 1]} {parts[0]}";
        return key;
    }

    public static double SaldoDe(Cliente c) =>
        c?.Movimientos == null ? 0 : c.Movimientos.Sum(m => m.Tipo == "cargo" ? m.Monto : -m.Monto);

    public static Grupo? GrupoDe(Cliente c, IEnumerable<Grupo> grupos) =>
        grupos?.FirstOrDefault(g => g.Id == c.GrupoId);

    public static double CuotaDe(Cliente c, IEnumerable<Grupo> grupos, Config cfg) =>
        (GrupoDe(c, grupos)?.Cuota ?? 0) + (c.Anexos * cfg.Anexo);

    public static bool FacturadoMes(Cliente c, string mes)
    {
        if (c.Meses.Contains(mes)) return true;
        if (c.Movimientos.Any(m => m.Tipo == "cargo" && m.Mes == mes)) return true;
        return false;
    }

    /// <summary>Aplica los pagos a los cargos por orden de fecha y devuelve lo que queda sin cubrir.</summary>
    public static List<Movimiento> PendientesDe(Cliente c)
    {
        var outList = new List<Movimiento>();
        var cargos = c.Movimientos.Where(m => m.Tipo == "cargo").OrderBy(m => m.Fecha).ThenBy(m => m.Id).ToList();
        double pagado = c.Movimientos.Where(m => m.Tipo == "pago").Sum(m => m.Monto);

        foreach (var m in cargos)
        {
            double cubre = Math.Min(pagado, m.Monto);
            pagado -= cubre;
            double resto = m.Monto - cubre;
            if (resto > 0)
            {
                outList.Add(new Movimiento
                {
                    Id = m.Id,
                    ClienteId = m.ClienteId,
                    Concepto = m.Concepto,
                    Fecha = m.Fecha,
                    Mes = m.Mes,
                    Monto = m.Monto,
                    Tipo = m.Tipo,
                    CotizacionEuro = m.CotizacionEuro,
                    Resto = resto
                });
            }
        }
        return outList;
    }

    public static List<Movimiento> ExigiblesDe(Cliente c, string mesActual) =>
        PendientesDe(c).Where(p => !(c.MesVencido && p.Mes == mesActual)).ToList();

    public static double TotalDe(IEnumerable<Movimiento> items) => items.Sum(x => x.Resto);

    // ── Papelera ────────────────────────────────────────────────
    public const int DiasRetencionPapelera = 30;
    public const int MaxItemsPapelera = 200;

    /// <summary>Movimientos de la papelera que ya pasaron el tiempo de retención o sobran por tamaño.</summary>
    public static List<MovimientoEliminado> VencidosDePapelera(List<MovimientoEliminado> papelera)
    {
        var limite = DateTime.Now.AddDays(-DiasRetencionPapelera);
        var vencidos = papelera
            .Where(e => DateTime.TryParseExact(e.EliminadoEl, "dd/MM/yyyy HH:mm",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) && f < limite)
            .ToList();

        var ordenados = papelera.OrderByDescending(e => e.EliminadoEl).ToList();
        for (int i = MaxItemsPapelera; i < ordenados.Count; i++)
            if (!vencidos.Contains(ordenados[i])) vencidos.Add(ordenados[i]);

        return vencidos;
    }
}
