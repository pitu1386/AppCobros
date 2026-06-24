namespace AppCobros.Services;

public class WhatsAppService : IWhatsAppService
{
    public async Task SendMessageAsync(string phone, string message)
    {
        // Limpiar teléfono
        var digitsOnly = new string(phone.Where(char.IsDigit).ToArray());
        if (string.IsNullOrEmpty(digitsOnly))
            return;

        var url = $"https://wa.me/{digitsOnly}?text={Uri.EscapeDataString(message)}";
        await Launcher.OpenAsync(url);
    }
}
