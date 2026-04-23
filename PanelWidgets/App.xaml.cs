using Microsoft.UI.Xaml;
using PanelWidgets.Settings;

namespace PanelWidgets;

public partial class App : Application
{
    private SettingsWindow? _settingsWindow;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _settingsWindow = new SettingsWindow();
        _settingsWindow.Activate();
    }
}
