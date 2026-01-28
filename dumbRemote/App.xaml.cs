namespace dumbRemote
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            MainPage = new AppShell();

            RequestedThemeChanged += (s, e) => StatusBarHelper.UpdateStatusBarColors(e.RequestedTheme);
        }
    }
}
