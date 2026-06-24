using AppCobros.PageModels;

namespace AppCobros.Pages;

public partial class AjustesPage : ContentPage
{
    private readonly AjustesViewModel _viewModel;

    public AjustesPage(AjustesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadDataAsync();
    }
}
