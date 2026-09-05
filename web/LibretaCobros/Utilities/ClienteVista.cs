using LibretaCobros.Models;

namespace LibretaCobros.Utilities;

/// <summary>Datos derivados de un cliente para pintar filas y encabezados sin recalcular en cada componente.</summary>
public class ClienteVista
{
    public Cliente Cliente { get; }
    public string Iniciales { get; }
    public string Detalle { get; }
    public double Saldo { get; }
    public double SaldoAbs => Math.Abs(Saldo);
    public double Exigible { get; }
    public bool TieneDeuda => Saldo > 0;
    public bool EsExigible => Exigible > 0;
    public string SaldoTexto => CobrosHelper.FormatMoney(SaldoAbs);
    public string Estado { get; }

    public ClienteVista(Cliente c, IReadOnlyList<Grupo> grupos, Config cfg, string mesActual)
    {
        Cliente = c;
        var g = CobrosHelper.GrupoDe(c, grupos);

        Iniciales = string.Join("", c.Nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2).Select(w => char.ToUpperInvariant(w[0])));

        var anexoTxt = c.Anexos > 0 ? $" · {c.Anexos} anexo{(c.Anexos > 1 ? "s" : "")}" : "";
        Detalle = $"{(g?.Nombre ?? "sin grupo")}{anexoTxt} · cuota {CobrosHelper.FormatMoney(CobrosHelper.CuotaDe(c, grupos, cfg))}";

        Saldo = CobrosHelper.SaldoDe(c);
        Exigible = CobrosHelper.TotalDe(CobrosHelper.ExigiblesDe(c, mesActual));

        Estado = Saldo > 0 ? (Exigible > 0 ? "debe" : "mes en curso")
               : Saldo < 0 ? "a favor" : "al día";
    }

    public static string MesActual => CobrosHelper.MesKey(DateTime.Now);
}
