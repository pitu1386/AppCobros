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
}
