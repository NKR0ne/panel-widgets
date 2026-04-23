using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PanelWidgets.Services;
using Windows.Storage;

namespace PanelWidgets.Settings;

public sealed partial class SettingsWindow : Window
{
    private readonly ApplicationDataContainer _settings =
        ApplicationData.Current.LocalSettings;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        var s = _settings.Values;
        TomTomKeyBox.Password   = s["TomTomApiKey"]          as string ?? "";
        OriginBox.Text          = s["TrafficOrigin"]         as string ?? "";
        OriginLabelBox.Text     = s["TrafficOriginLabel"]    as string ?? "";
        DestBox.Text            = s["TrafficDestination"]    as string ?? "";
        DestLabelBox.Text       = s["TrafficDestLabel"]      as string ?? "";
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        SignInButton.IsEnabled  = false;
        AuthStatus.IsOpen       = false;
        try
        {
            await GraphService.Default.SignInAsync();
            AuthStatus.Severity  = InfoBarSeverity.Success;
            AuthStatus.Message   = "Signed in successfully. Agenda and To-Do widgets will refresh shortly.";
            AuthStatus.IsOpen    = true;
            SignOutButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            AuthStatus.Severity = InfoBarSeverity.Error;
            AuthStatus.Message  = $"Sign-in failed: {ex.Message}";
            AuthStatus.IsOpen   = true;
            SignInButton.IsEnabled = true;
        }
    }

    private async void SignOut_Click(object sender, RoutedEventArgs e)
    {
        await GraphService.Default.SignOutAsync();
        AuthStatus.Severity     = InfoBarSeverity.Informational;
        AuthStatus.Message      = "Signed out. Agenda and To-Do widgets will show an error until you sign in again.";
        AuthStatus.IsOpen       = true;
        SignOutButton.IsEnabled = false;
        SignInButton.IsEnabled  = true;
    }

    // ── Traffic settings ─────────────────────────────────────────────────────

    private void TomTomKey_Changed(object sender, RoutedEventArgs e) =>
        Save("TomTomApiKey", TomTomKeyBox.Password);

    private void TrafficSettings_Changed(object sender, TextChangedEventArgs e)
    {
        Save("TrafficOrigin",      OriginBox.Text);
        Save("TrafficOriginLabel", OriginLabelBox.Text);
        Save("TrafficDestination", DestBox.Text);
        Save("TrafficDestLabel",   DestLabelBox.Text);
    }

    private void Save(string key, string value) =>
        _settings.Values[key] = value;
}
