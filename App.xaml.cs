using Microsoft.Extensions.DependencyInjection;
using AppCobros.Pages;
using Plugin.Fingerprint;

namespace AppCobros
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        protected override async void OnStart()
        {
            base.OnStart();
            await LockAppAsync();
        }

        protected override async void OnResume()
        {
            base.OnResume();
            await LockAppAsync();
        }

        private async Task LockAppAsync()
        {
            var page = Windows.Count > 0 ? Windows[0].Page : null;
            if (page != null)
            {
#if !WINDOWS
                var isAvailable = await CrossFingerprint.Current.IsAvailableAsync(true);
                if (isAvailable)
                {
                    // Avoid pushing multiple auth pages if one is already there
                    if (page.Navigation.ModalStack.Count == 0 || !(page.Navigation.ModalStack.LastOrDefault() is AuthPage))
                    {
                        await page.Navigation.PushModalAsync(new AuthPage(), animated: false);
                    }
                }
#endif
            }
        }
    }
}