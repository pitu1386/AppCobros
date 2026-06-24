namespace AppCobros.Services;

public interface IWhatsAppService
{
    Task SendMessageAsync(string phone, string message);
}
