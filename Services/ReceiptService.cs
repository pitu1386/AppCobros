using SkiaSharp;
using AppCobros.Models;

namespace AppCobros.Services;

public interface IReceiptService
{
    Task<string> GenerateReceiptAsync(Client client, Movimiento pago);
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
}
