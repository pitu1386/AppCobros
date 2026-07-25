using SkiaSharp;
using AppCobros.Models;
using AppCobros.Utilities;

namespace AppCobros.Services;

public interface IReceiptService
{
    Task<string> GenerateReceiptAsync(Client client, Movimiento pago);

    /// Resumen de cuenta con el historial completo del cliente, listo para compartir.
    Task<string> GenerateAccountStatementAsync(Client client, IEnumerable<Grupo> grupos, Config config);
}

public class ReceiptService : IReceiptService
{
    public Task<string> GenerateReceiptAsync(Client client, Movimiento pago)
    {
        int width = 800;
        int height = 1100;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;

        // ── Background ──────────────────────────────────────────
        canvas.Clear(SKColor.Parse("#F4F3FB"));

        // ── Header card ─────────────────────────────────────────
        var headerPaint = new SKPaint { Color = SKColor.Parse("#221C44"), IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(40, 40, width - 40, 240), 20), headerPaint);

        using var titleFont = new SKFont(
            SKTypeface.FromFamilyName("sans-serif", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 52);
        using var titlePaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        canvas.DrawText("RECIBO DE PAGO", width / 2f, 145, SKTextAlign.Center, titleFont, titlePaint);

        using var dateFont = new SKFont(SKTypeface.FromFamilyName("sans-serif"), 28);
        using var datePaint = new SKPaint
        {
            Color = SKColor.Parse("#FFB547"),
            IsAntialias = true
        };
        canvas.DrawText(pago.Fecha, width / 2f, 200, SKTextAlign.Center, dateFont, datePaint);

        // ── Body card ───────────────────────────────────────────
        var cardPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(40, 270, width - 40, 870), 20), cardPaint);

        using var labelFont = new SKFont(SKTypeface.FromFamilyName("sans-serif"), 26);
        using var labelPaint = new SKPaint { Color = SKColor.Parse("#75718F"), IsAntialias = true };
        using var valueFont = new SKFont(SKTypeface.FromFamilyName("sans-serif", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 42);
        using var valuePaint = new SKPaint { Color = SKColor.Parse("#221C44"), IsAntialias = true };
        using var amountFont = new SKFont(SKTypeface.FromFamilyName("sans-serif", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), 68);
        using var amountPaint = new SKPaint { Color = SKColor.Parse("#0FA36B"), IsAntialias = true };
        using var linePaint = new SKPaint { Color = SKColor.Parse("#DEDCEE"), StrokeWidth = 1.5f, IsAntialias = true };

        canvas.DrawText("RECIBÍ DE", 80, 325, SKTextAlign.Left, labelFont, labelPaint);
        canvas.DrawText(client.Nombre.ToUpper(), 80, 380, SKTextAlign.Left, valueFont, valuePaint);

        canvas.DrawLine(80, 430, width - 80, 430, linePaint);

        canvas.DrawText("LA SUMA DE", 80, 480, SKTextAlign.Left, labelFont, labelPaint);
        string montoStr = "$ " + Math.Round(pago.Monto).ToString("N0", new System.Globalization.CultureInfo("es-AR"));
        canvas.DrawText(montoStr, 80, 565, SKTextAlign.Left, amountFont, amountPaint);

        canvas.DrawLine(80, 610, width - 80, 610, linePaint);

        canvas.DrawText("EN CONCEPTO DE", 80, 660, SKTextAlign.Left, labelFont, labelPaint);
        canvas.DrawText(pago.Concepto.ToUpper(), 80, 710, SKTextAlign.Left, valueFont, valuePaint);

        canvas.DrawLine(80, 760, width - 80, 760, linePaint);

        canvas.DrawText("FECHA", 80, 810, SKTextAlign.Left, labelFont, labelPaint);
        canvas.DrawText(DateTime.Now.ToString("dd/MM/yyyy HH:mm"), 80, 860, SKTextAlign.Left, valueFont, valuePaint);

        // ── Footer ──────────────────────────────────────────────
        using var footerFont = new SKFont(SKTypeface.FromFamilyName("sans-serif"), 24);
        using var footerPaint = new SKPaint { Color = SKColor.Parse("#8D88B5"), IsAntialias = true };
        canvas.DrawText("¡Muchas gracias por su pago!", width / 2f, 940, SKTextAlign.Center, footerFont, footerPaint);
        canvas.DrawText("Comprobante generado automáticamente", width / 2f, 980, SKTextAlign.Center, footerFont, footerPaint);

        // ── Save PNG ─────────────────────────────────────────────
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 95);

        string fileName = $"Recibo_{client.Nombre.Replace(" ", "")}_{DateTime.Now:yyyyMMddHHmmss}.png";
        string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);

        return Task.FromResult(filePath);
    }

    // Cantidad máxima de movimientos que entran en el resumen para que la imagen no crezca sin control.
    private const int MaxMovimientosEstadoCuenta = 40;

    public Task<string> GenerateAccountStatementAsync(Client client, IEnumerable<Grupo> grupos, Config config)
    {
        var listaGrupos = grupos as IList<Grupo> ?? grupos.ToList();
        var mesActual = CobrosHelper.MesKey(DateTime.Now);

        var todos = client.Movimientos
            .OrderByDescending(m => m.Fecha)
            .ThenByDescending(m => m.Id)
            .ToList();
        var movimientos = todos.Take(MaxMovimientosEstadoCuenta).ToList();
        int ocultos = todos.Count - movimientos.Count;

        double saldo = CobrosHelper.SaldoDe(client);
        double exigible = CobrosHelper.TotalDe(CobrosHelper.ExigiblesDe(client, mesActual));
        double cuota = CobrosHelper.CuotaDe(client, listaGrupos, config);
        var grupo = CobrosHelper.GrupoDe(client, listaGrupos);

        const int width = 800;
        const int rowHeight = 46;
        const int tablaTop = 630;
        const int tablaHeaderHeight = 54;

        int filasTop = tablaTop + tablaHeaderHeight;
        int filasHeight = Math.Max(movimientos.Count, 1) * rowHeight;
        int totalTop = filasTop + filasHeight + 16;
        int height = totalTop + 70 + (ocultos > 0 ? 34 : 0) + 90;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColor.Parse("#F4F3FB"));

        var tinta = SKColor.Parse("#221C44");
        var apagado = SKColor.Parse("#75718F");
        var verde = SKColor.Parse("#0FA36B");
        var rojo = SKColor.Parse("#E14D43");
        var linea = SKColor.Parse("#DEDCEE");

        using var bold28 = FuenteBold(28);
        using var bold24 = FuenteBold(24);
        using var bold34 = FuenteBold(34);
        using var bold44 = FuenteBold(44);
        using var titulo = FuenteBold(50);
        using var normal24 = Fuente(24);
        using var normal22 = Fuente(22);
        using var chico20 = Fuente(20);

        using var pTinta = new SKPaint { Color = tinta, IsAntialias = true };
        using var pApagado = new SKPaint { Color = apagado, IsAntialias = true };
        using var pBlanco = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var pVerde = new SKPaint { Color = verde, IsAntialias = true };
        using var pRojo = new SKPaint { Color = rojo, IsAntialias = true };
        using var pAmbar = new SKPaint { Color = SKColor.Parse("#FFB547"), IsAntialias = true };
        using var pLinea = new SKPaint { Color = linea, StrokeWidth = 1.5f, IsAntialias = true };
        using var pCard = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var pHeader = new SKPaint { Color = tinta, IsAntialias = true };
        using var pFilaAlterna = new SKPaint { Color = SKColor.Parse("#FAF9FF"), IsAntialias = true };

        // ── Encabezado ──────────────────────────────────────────
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(40, 40, width - 40, 240), 20), pHeader);
        canvas.DrawText("ESTADO DE CUENTA", width / 2f, 140, SKTextAlign.Center, titulo, pBlanco);
        canvas.DrawText($"Emitido el {DateTime.Now:dd/MM/yyyy HH:mm}", width / 2f, 195, SKTextAlign.Center, normal24, pAmbar);

        // ── Datos del cliente ───────────────────────────────────
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(40, 270, width - 40, 440), 20), pCard);
        canvas.DrawText("CLIENTE", 80, 320, SKTextAlign.Left, chico20, pApagado);
        canvas.DrawText(Recortar(client.Nombre.ToUpper(), bold34, width - 160), 80, 368, SKTextAlign.Left, bold34, pTinta);

        string detalle = grupo != null ? grupo.Nombre : "Sin grupo";
        detalle += $" · cuota {CobrosHelper.FormatMoney(cuota)}";
        if (client.Anexos > 0) detalle += $" · {client.Anexos} anexo{(client.Anexos > 1 ? "s" : "")}";
        if (!string.IsNullOrWhiteSpace(client.Telefono)) detalle += $" · {client.Telefono}";
        canvas.DrawText(Recortar(detalle, normal22, width - 160), 80, 410, SKTextAlign.Left, normal22, pApagado);

        // ── Resumen ─────────────────────────────────────────────
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(40, 470, width - 40, 600), 20), pCard);

        canvas.DrawText("SALDO DE LA CUENTA", 80, 512, SKTextAlign.Left, chico20, pApagado);
        canvas.DrawText(CobrosHelper.FormatMoney(Math.Abs(saldo)), 80, 566, SKTextAlign.Left, bold44,
            saldo > 0 ? pRojo : pVerde);

        canvas.DrawText("TOTAL A RECLAMAR", width - 80, 512, SKTextAlign.Right, chico20, pApagado);
        canvas.DrawText(CobrosHelper.FormatMoney(exigible), width - 80, 566, SKTextAlign.Right, bold34,
            exigible > 0 ? pRojo : pVerde);

        string estado = exigible > 0 ? "DEBE" : saldo > 0 ? "MES EN CURSO" : saldo < 0 ? "A FAVOR" : "AL DÍA";
        canvas.DrawText(estado, width / 2f, 590, SKTextAlign.Center, chico20, pApagado);

        // ── Tabla de movimientos ────────────────────────────────
        canvas.DrawRoundRect(new SKRoundRect(new SKRect(40, tablaTop, width - 40, totalTop + 60), 20), pCard);

        canvas.DrawText("DETALLE DE MOVIMIENTOS", 80, tablaTop + 40, SKTextAlign.Left, bold24, pTinta);
        canvas.DrawLine(80, tablaTop + tablaHeaderHeight - 2, width - 80, tablaTop + tablaHeaderHeight - 2, pLinea);

        if (movimientos.Count == 0)
        {
            canvas.DrawText("Sin movimientos registrados.", 80, filasTop + 30, SKTextAlign.Left, normal22, pApagado);
        }
        else
        {
            for (int i = 0; i < movimientos.Count; i++)
            {
                var m = movimientos[i];
                float top = filasTop + i * rowHeight;
                float baseline = top + 30;

                if (i % 2 == 1)
                    canvas.DrawRect(new SKRect(60, top, width - 60, top + rowHeight), pFilaAlterna);

                canvas.DrawText(m.FechaCorta, 80, baseline, SKTextAlign.Left, chico20, pApagado);
                canvas.DrawText(Recortar(m.Concepto, normal22, 380), 190, baseline, SKTextAlign.Left, normal22, pTinta);
                canvas.DrawText(m.MontoConSigno, width - 80, baseline, SKTextAlign.Right, bold24,
                    m.Tipo == "cargo" ? pRojo : pVerde);
            }
        }

        // ── Total ───────────────────────────────────────────────
        canvas.DrawLine(80, totalTop, width - 80, totalTop, pLinea);
        canvas.DrawText(saldo >= 0 ? "SALDO PENDIENTE" : "SALDO A FAVOR", 80, totalTop + 44, SKTextAlign.Left, bold28, pTinta);
        canvas.DrawText(CobrosHelper.FormatMoney(Math.Abs(saldo)), width - 80, totalTop + 44, SKTextAlign.Right, bold34,
            saldo > 0 ? pRojo : pVerde);

        float pieY = totalTop + 90;
        if (ocultos > 0)
        {
            canvas.DrawText($"Se muestran los {MaxMovimientosEstadoCuenta} movimientos más recientes ({ocultos} anteriores no incluidos).",
                80, pieY, SKTextAlign.Left, chico20, pApagado);
            pieY += 34;
        }

        canvas.DrawText("Resumen generado automáticamente por Libreta de Cobros.", width / 2f, pieY + 30, SKTextAlign.Center, chico20, pApagado);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 95);

        string fileName = $"EstadoCuenta_{Limpiar(client.Nombre)}_{DateTime.Now:yyyyMMddHHmmss}.png";
        string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

        using var stream = File.OpenWrite(filePath);
        data.SaveTo(stream);

        return Task.FromResult(filePath);
    }

    private static SKFont Fuente(float size) => new(SKTypeface.FromFamilyName("sans-serif"), size);

    private static SKFont FuenteBold(float size) => new(
        SKTypeface.FromFamilyName("sans-serif", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright), size);

    /// Corta el texto con puntos suspensivos para que nunca se pise con la columna de al lado.
    private static string Recortar(string texto, SKFont font, float anchoMaximo)
    {
        if (string.IsNullOrEmpty(texto) || font.MeasureText(texto) <= anchoMaximo) return texto;

        var recortado = texto;
        while (recortado.Length > 1 && font.MeasureText(recortado + "…") > anchoMaximo)
            recortado = recortado[..^1];

        return recortado + "…";
    }

    private static string Limpiar(string nombre)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpio = new string(nombre.Where(c => !invalidos.Contains(c)).ToArray());
        return limpio.Replace(" ", "");
    }
}
