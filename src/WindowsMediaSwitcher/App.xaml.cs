using Microsoft.UI.Xaml;
using WindowsMediaSwitcher.Services;
using WindowsMediaSwitcher.Views;

namespace WindowsMediaSwitcher;

public partial class App : Application
{
    private Window? _mainWindow;

    public static SettingsService Settings { get; } = new();
    public static AudioDeviceService Audio { get; } = new();
    public static AutostartService Autostart { get; } = new();
    public static HotkeyService Hotkey { get; } = new();

    public static Window? MainHost => ((App)Current)._mainWindow;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => { e.Handled = true; };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Autostart.SetEnabled(Settings.Current.StartWithWindows);
        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}
