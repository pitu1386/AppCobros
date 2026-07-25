using System.Globalization;
using AppCobros.Models;

namespace AppCobros.Utilities;

public static class CobrosHelper
{
    public static readonly string[] MESES = { "enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre" };

    public static string FormatMoney(double amount)
    {
        return "$ " + Math.Round(amount).ToString("N0", new CultureInfo("es-AR"));
    }

    public static string HoyISO() => DateTime.Now.ToString("yyyy-MM-dd");

    public static string MesKey(DateTime date) => date.ToString("yyyy-MM");

    public static string MesLabel(string key)
    {
        if (string.IsNullOrEmpty(key)) return "";
        var parts = key.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[1], out int month))
        {
            return $"{MESES[month - 1]} {parts[0]}";
        }
        return key;
    }

    public static double SaldoDe(Client c)
    {
        if (c == null || c.Movimientos == null) return 0;
        return c.Movimientos.Sum(m => m.Tipo == "cargo" ? m.Monto : -m.Monto);
    }

    public static Grupo? GrupoDe(Client c, IEnumerable<Grupo> grupos)
    {
        return grupos?.FirstOrDefault(g => g.Id == c.GrupoId);
    }

    public static double CuotaDe(Client c, IEnumerable<Grupo> grupos, Config cfg)
    {
        var g = GrupoDe(c, grupos);
        return (g?.Cuota ?? 0) + (c.Anexos * cfg.Anexo);
    }

    public static bool FacturadoMes(Client c, string mes)
    {
        if (c.Meses != null && c.Meses.Contains(mes)) return true;
        if (c.Movimientos != null && c.Movimientos.Any(m => m.Tipo == "cargo" && m.Mes == mes)) return true;
        return false;
    }

    public static List<Movimiento> PendientesDe(Client c)
    {
        var outList = new List<Movimiento>();
        if (c.Movimientos == null) return outList;

        var cargos = c.Movimientos.Where(m => m.Tipo == "cargo").OrderBy(m => m.Fecha).ThenBy(m => m.Id).ToList();
        double pagado = c.Movimientos.Where(m => m.Tipo == "pago").Sum(m => m.Monto);

        foreach (var m in cargos)
        {
            double cubre = Math.Min(pagado, m.Monto);
            pagado -= cubre;
            double resto = m.Monto - cubre;
            if (resto > 0)
            {
                var copy = new Movimiento
                {
                    Id = m.Id,
                    Concepto = m.Concepto,
                    Fecha = m.Fecha,
                    Mes = m.Mes,
                    Monto = m.Monto,
                    Tipo = m.Tipo,
                    Resto = resto
                };
                outList.Add(copy);
            }
        }
        return outList;
    }

    public static List<Movimiento> ExigiblesDe(Client c, string mesActual)
    {
        return PendientesDe(c).Where(p => !(c.MesVencido && p.Mes == mesActual)).ToList();
    }

    public static double TotalDe(IEnumerable<Movimiento> items)
    {
        return items.Sum(x => x.Resto);
    }

    // ── Papelera ────────────────────────────────────────────────

    /// Cuántos días se conservan los movimientos borrados antes de descartarlos solos.
    public const int DiasRetencionPapelera = 30;
    private const int MaxItemsPapelera = 200;

    /// Manda un movimiento a la papelera dejando el cliente consistente (libera el mes si era una cuota).
    public static MovimientoEliminado EnviarAPapelera(CobrosData data, Client cliente, Movimiento movimiento)
    {
        string? mesLiberado = null;
        if (movimiento.Tipo == "cargo" && !string.IsNullOrEmpty(movimiento.Mes) && cliente.Meses.Contains(movimiento.Mes))
            mesLiberado = movimiento.Mes;

        cliente.Movimientos.Remove(movimiento);
        if (mesLiberado != null) cliente.Meses.Remove(mesLiberado);

        var eliminado = new MovimientoEliminado
        {
            ClienteId = cliente.Id,
            ClienteNombre = cliente.Nombre,
            EliminadoEl = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            MesLiberado = mesLiberado,
            Movimiento = movimiento
        };

        data.Papelera.Insert(0, eliminado);
        PurgarPapelera(data);
        return eliminado;
    }

    /// Devuelve el movimiento a la cuenta del cliente. False si el cliente ya no existe.
    public static bool RestaurarDePapelera(CobrosData data, MovimientoEliminado eliminado)
    {
        var cliente = data.Clients.FirstOrDefault(c => c.Id == eliminado.ClienteId);
        if (cliente == null) return false;

        if (!cliente.Movimientos.Any(m => m.Id == eliminado.Movimiento.Id))
            cliente.Movimientos.Add(eliminado.Movimiento);

        if (!string.IsNullOrEmpty(eliminado.MesLiberado) && !cliente.Meses.Contains(eliminado.MesLiberado))
            cliente.Meses.Add(eliminado.MesLiberado);

        data.Papelera.Remove(eliminado);
        return true;
    }

    /// Descarta lo que ya pasó el tiempo de retención y recorta la lista si creció demasiado.
    public static void PurgarPapelera(CobrosData data)
    {
        var limite = DateTime.Now.AddDays(-DiasRetencionPapelera);

        var vencidos = data.Papelera
            .Where(e => DateTime.TryParseExact(e.EliminadoEl, "dd/MM/yyyy HH:mm",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
                        && fecha < limite)
            .ToList();

        foreach (var v in vencidos) data.Papelera.Remove(v);

        while (data.Papelera.Count > MaxItemsPapelera)
            data.Papelera.RemoveAt(data.Papelera.Count - 1);
    }
}
