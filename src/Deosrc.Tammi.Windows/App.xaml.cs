using AppInfo = TAMMi.Core.AppInfo;

namespace TAMMi.Windows;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage()) { Title = AppInfo.Name };
    }
}
