using AppCobros.PageModels;

namespace AppCobros.Pages;

public partial class PapeleraPage : ContentPage
{
    private readonly PapeleraViewModel _viewModel;

    public PapeleraPage(PapeleraViewModel viewModel)
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
