using AppCobros.PageModels;

namespace AppCobros.Pages;

public partial class ClienteFormPage : ContentPage
{
    private readonly ClienteFormViewModel _viewModel;

    public ClienteFormPage(ClienteFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
