using AppCobros.PageModels;

namespace AppCobros.Pages;

public partial class ReclamarMasivoPage : ContentPage
{
    private readonly ReclamarMasivoViewModel _viewModel;

    public ReclamarMasivoPage(ReclamarMasivoViewModel viewModel)
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
