using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using AppCobros.Services;

namespace AppCobros;

/// Widget de pantalla de inicio con la deuda total y lo cobrado en el mes.
/// Los valores los deja calculados <see cref="WidgetService"/> cada vez que se guardan datos.
[BroadcastReceiver(Label = "Libreta de Cobros", Exported = true)]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_deuda_info")]
public class DeudaWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context == null || appWidgetManager == null || appWidgetIds == null) return;

        var views = ConstruirVista(context);
        foreach (var id in appWidgetIds)
            appWidgetManager.UpdateAppWidget(id, views);
    }

    /// Redibuja todos los widgets instalados. Se llama después de guardar datos.
    public static void RefrescarTodos()
    {
        var context = Android.App.Application.Context;
        var manager = AppWidgetManager.GetInstance(context);
        if (manager == null) return;

        var componente = new ComponentName(context, Java.Lang.Class.FromType(typeof(DeudaWidgetProvider)).CanonicalName!);
        var ids = manager.GetAppWidgetIds(componente);
        if (ids == null || ids.Length == 0) return;

        var views = ConstruirVista(context);
        foreach (var id in ids)
            manager.UpdateAppWidget(id, views);
    }

    private static RemoteViews ConstruirVista(Context context)
    {
        var views = new RemoteViews(context.PackageName, Resource.Layout.widget_deuda);

        string deuda = Preferences.Default.Get(WidgetService.KeyDeuda, "$ 0");
        string cobrado = Preferences.Default.Get(WidgetService.KeyCobrado, "$ 0");
        int morosos = Preferences.Default.Get(WidgetService.KeyMorosos, 0);
        string actualizado = Preferences.Default.Get(WidgetService.KeyActualizado, string.Empty);

        views.SetTextViewText(Resource.Id.widget_deuda, deuda);
        views.SetTextViewText(Resource.Id.widget_morosos,
            morosos == 0 ? "Nadie con deuda exigible ✅" : $"{morosos} cliente(s) con deuda");
        views.SetTextViewText(Resource.Id.widget_cobrado, $"Cobrado este mes: {cobrado}");
        views.SetTextViewText(Resource.Id.widget_actualizado,
            string.IsNullOrEmpty(actualizado) ? "Abrí la app para actualizar" : $"Actualizado {actualizado}");

        var intent = new Intent(context, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
        var pendiente = PendingIntent.GetActivity(context, 0, intent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        views.SetOnClickPendingIntent(Resource.Id.widget_root, pendiente);

        return views;
    }
}
