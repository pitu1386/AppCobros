using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;

namespace AppCobros
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
    				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
    				{
    					handler.PlatformView.SingleSelectionFollowsFocus = false;
    				});

    				Microsoft.Maui.Handlers.ContentViewHandler.Mapper.AppendToMapping(nameof(Pages.Controls.CategoryChart), (handler, view) =>
    				{
    					if (view is Pages.Controls.CategoryChart && handler.PlatformView is Microsoft.Maui.Platform.ContentPanel contentPanel)
    					{
    						contentPanel.IsTabStop = true;
    					}
    				});
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    // removed FluentUI font which might be missing, or rather let's keep it if it was there
                    // fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            // Registramos solo los servicios de Cobros, removí los repositorios viejos para evitar errores si no los implementé.
            // Si ya estaban en la plantilla, mejor los dejo para evitar que se rompa otra cosa. Pero como yo estoy haciendo la app de cero, no los necesito.
            builder.Services.AddSingleton<AppCobros.Services.IDataService, AppCobros.Services.DataService>();
            builder.Services.AddSingleton<AppCobros.Services.IWhatsAppService, AppCobros.Services.WhatsAppService>();
            builder.Services.AddSingleton<AppCobros.Services.IReceiptService, AppCobros.Services.ReceiptService>();

            builder.Services.AddTransient<AppCobros.PageModels.DashboardViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.InicioViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.ClientesViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.AjustesViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.ClienteDetalleViewModel>();

            builder.Services.AddTransient<AppCobros.Pages.DashboardPage>();
            builder.Services.AddTransient<AppCobros.Pages.InicioPage>();
            builder.Services.AddTransient<AppCobros.Pages.ClientesPage>();
            builder.Services.AddTransient<AppCobros.Pages.AjustesPage>();
            builder.Services.AddTransient<AppCobros.Pages.ClienteDetallePage>();

            return builder.Build();
        }
    }
}
