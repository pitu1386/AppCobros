namespace AppCobros
{
    public partial class App : Application
    {
        public App()
        {
            var tema = Preferences.Default.Get("tema", 0);
            UserAppTheme = tema switch
            {
                1 => AppTheme.Light,
                2 => AppTheme.Dark,
                _ => AppTheme.Unspecified
            };
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}