using AppCobros.PageModels;

namespace AppCobros.Pages;

public partial class ClienteDetallePage : ContentPage
{
    private readonly ClienteDetalleViewModel _viewModel;

    public ClienteDetallePage(ClienteDetalleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
}
