using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace AppCobros.Pages;

public partial class AuthPage : ContentPage
{
    public AuthPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await AuthenticateAsync();
    }

    private async void UnlockBtn_Clicked(object? sender, EventArgs e)
    {
        await AuthenticateAsync();
    }

    private async Task AuthenticateAsync()
    {
        var request = new AuthenticationRequestConfiguration("Desbloquear AppCobros", "Verificá tu identidad para acceder a la aplicación.");
        var result = await CrossFingerprint.Current.AuthenticateAsync(request);

        if (result.Authenticated)
        {
            // Close the modal and let the user in
            await Navigation.PopModalAsync(animated: false);
        }
        else
        {
            // Failed
            await DisplayAlertAsync("Error", "No se pudo verificar la identidad.", "OK");
        }
    }
}
