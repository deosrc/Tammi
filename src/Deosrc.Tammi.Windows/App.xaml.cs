using AppInfo = Deosrc.Tammi.Core.AppInfo;

namespace Deosrc.Tammi.Windows;

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
