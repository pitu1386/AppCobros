using AppCobros.Models;
using AppCobros.PageModels;

namespace AppCobros.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}