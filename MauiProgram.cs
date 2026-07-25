using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

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
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
    				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
    				{
    					handler.PlatformView.SingleSelectionFollowsFocus = false;
    				});
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            builder.Services.AddSingleton<AppCobros.Services.IDataService, AppCobros.Services.DataService>();
            builder.Services.AddSingleton<AppCobros.Services.IWhatsAppService, AppCobros.Services.WhatsAppService>();
            builder.Services.AddSingleton<AppCobros.Services.IReceiptService, AppCobros.Services.ReceiptService>();

            builder.Services.AddTransient<AppCobros.PageModels.DashboardViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.InicioViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.ClientesViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.AjustesViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.ClienteDetalleViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.ClienteFormViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.ReclamarMasivoViewModel>();
            builder.Services.AddTransient<AppCobros.PageModels.PapeleraViewModel>();

            builder.Services.AddTransient<AppCobros.Pages.DashboardPage>();
            builder.Services.AddTransient<AppCobros.Pages.InicioPage>();
            builder.Services.AddTransient<AppCobros.Pages.ClientesPage>();
            builder.Services.AddTransient<AppCobros.Pages.AjustesPage>();
            builder.Services.AddTransient<AppCobros.Pages.ClienteDetallePage>();
            builder.Services.AddTransient<AppCobros.Pages.ClienteFormPage>();
            builder.Services.AddTransient<AppCobros.Pages.ReclamarMasivoPage>();
            builder.Services.AddTransient<AppCobros.Pages.PapeleraPage>();

            return builder.Build();
        }
    }
}
