using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Font = Microsoft.Maui.Font;

namespace AppCobros
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(ClienteDetallePage), typeof(ClienteDetallePage));
            Routing.RegisterRoute(nameof(ClienteFormPage), typeof(ClienteFormPage));
            Routing.RegisterRoute(nameof(ReclamarMasivoPage), typeof(ReclamarMasivoPage));
            Routing.RegisterRoute(nameof(PapeleraPage), typeof(PapeleraPage));
        }

        public static async Task DisplaySnackbarAsync(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#FF3300"),
                TextColor = Colors.White,
                ActionButtonTextColor = Colors.Yellow,
                CornerRadius = new CornerRadius(0),
                Font = Font.SystemFontOfSize(18),
                ActionButtonFont = Font.SystemFontOfSize(14)
            };

            var snackbar = Snackbar.Make(message, visualOptions: snackbarOptions);

            await snackbar.Show(cancellationTokenSource.Token);
        }

        /// Aviso con botón "Deshacer" para acciones destructivas reversibles.
        public static async Task DisplayUndoSnackbarAsync(string message, Func<Task> onUndo)
        {
            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#221C44"),
                TextColor = Colors.White,
                ActionButtonTextColor = Color.FromArgb("#FFB547"),
                CornerRadius = new CornerRadius(10),
                Font = Font.SystemFontOfSize(14),
                ActionButtonFont = Font.SystemFontOfSize(14, FontWeight.Bold)
            };

            var snackbar = Snackbar.Make(
                message,
                action: () => _ = onUndo(),
                actionButtonText: "DESHACER",
                duration: TimeSpan.FromSeconds(6),
                visualOptions: snackbarOptions);

            await snackbar.Show();
        }

        public static async Task DisplayToastAsync(string message)
        {
            // Toast is currently not working in MCT on Windows
            if (OperatingSystem.IsWindows())
                return;

            var toast = Toast.Make(message, textSize: 18);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }
    }
}
