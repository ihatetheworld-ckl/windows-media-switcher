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
    public static UpdateService Updates { get; } = new();

    public static Window? MainHost => ((App)Current)._mainWindow;

    public App()
    {
        InitializeComponent();
        // Keep handled so tray host stays alive, but do not silently drop everything forever —
        // Settings open path now surfaces errors via tray tip.
        UnhandledException += (_, e) => { e.Handled = true; };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Autostart.SetEnabled(Settings.Current.StartWithWindows);
        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }
}
