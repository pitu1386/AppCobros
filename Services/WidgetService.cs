using AppCobros.Models;
using AppCobros.Utilities;

namespace AppCobros.Services;

/// Deja en Preferences los números que muestra el widget de la pantalla de inicio.
/// El widget lee valores ya calculados en vez de abrir y parsear el archivo de datos.
public static class WidgetService
{
    public const string KeyDeuda = "widget_deuda";
    public const string KeyCobrado = "widget_cobrado";
    public const string KeyMorosos = "widget_morosos";
    public const string KeyActualizado = "widget_actualizado";

    public static void Actualizar(CobrosData data)
    {
        try
        {
            var mesKey = CobrosHelper.MesKey(DateTime.Now);

            double deuda = 0;
            int morosos = 0;

            foreach (var c in data.Clients.Where(c => !c.Archivado))
            {
                double exigible = CobrosHelper.TotalDe(CobrosHelper.ExigiblesDe(c, mesKey));
                deuda += exigible;
                if (exigible > 0) morosos++;
            }

            double cobrado = data.Clients
                .SelectMany(c => c.Movimientos)
                .Where(m => m.Tipo == "pago" && m.Fecha.StartsWith(mesKey))
                .Sum(m => m.Monto);

            Preferences.Default.Set(KeyDeuda, CobrosHelper.FormatMoney(deuda));
            Preferences.Default.Set(KeyCobrado, CobrosHelper.FormatMoney(cobrado));
            Preferences.Default.Set(KeyMorosos, morosos);
            Preferences.Default.Set(KeyActualizado, DateTime.Now.ToString("dd/MM HH:mm"));

            Refrescar();
        }
        catch
        {
            // El widget nunca debe impedir que se guarden los datos.
        }
    }

    private static void Refrescar()
    {
#if ANDROID
        AppCobros.DeudaWidgetProvider.RefrescarTodos();
#endif
    }
}
