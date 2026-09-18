using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WindowsMediaSwitcher.Helpers;
using WinRT.Interop;

namespace WindowsMediaSwitcher.Views;

public sealed partial class MainWindow : Window
{
    private DevicePopupWindow? _popup;
    private SettingsWindow? _settings;
    private bool _hotkeyAttached;

    public ICommand ShowPopupCommand { get; }

    public MainWindow()
    {
        // x:Bind reads this during InitializeComponent — assign first
        ShowPopupCommand = new RelayCommand(ShowDevicePopup);
        InitializeComponent();

        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new SizeInt32(1, 1));
        AppWindow.Move(new PointInt32(-32000, -32000));

        try
        {
            if (AppWindow.Presenter is OverlappedPresenter op)
            {
                op.IsResizable = false;
                op.IsMaximizable = false;
                op.IsMinimizable = false;
                op.SetBorderAndTitleBar(false, false);
            }
        }
        catch { /* older SDK */ }

        Activated += OnActivated;
        Closed += OnClosed;

        TrySetTrayIcon();
        // Left click is bound via LeftClickCommand in XAML (H.NotifyIcon has no LeftClick event)

        App.Hotkey.HotkeyPressed += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(ShowDevicePopup);
        };
        App.Settings.SettingsChanged += (_, _) =>
        {
            DispatcherQueue.TryEnqueue(ReregisterHotkey);
        };
    }

    private void TrySetTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (!File.Exists(iconPath)) return;

            // Prefer path-based Icon API when present (H.NotifyIcon versions differ)
            var trayType = TrayIcon.GetType();
            var iconProp = trayType.GetProperty("Icon");
            if (iconProp?.PropertyType == typeof(string))
            {
                iconProp.SetValue(TrayIcon, iconPath);
                return;
            }

            TrayIcon.IconSource = new BitmapImage(new Uri(iconPath));
        }
        catch
        {
            // Tray still works with package default
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_hotkeyAttached) return;
        var hwnd = WindowNative.GetWindowHandle(this);
        if (hwnd == IntPtr.Zero) return;

        WindowNativeHelper.HideFromTaskbar(hwnd);
        App.Hotkey.Attach(hwnd);
        ReregisterHotkey();
        _hotkeyAttached = true;
        AppWindow.Hide();
    }

    private void ReregisterHotkey()
    {
        _ = App.Hotkey.Register(App.Settings.Current);
    }

    private void OnShowPopup(object sender, RoutedEventArgs e) => ShowDevicePopup();

    private void ShowDevicePopup()
    {
        if (_popup is { IsOpen: true })
        {
            _popup.ClosePopup();
            return;
        }

        _popup?.Close();
        _popup = new DevicePopupWindow();
        _popup.Closed += (_, _) => _popup = null;
        _popup.ShowPopup();
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        if (_settings is not null)
        {
            _settings.Activate();
            return;
        }
        _settings = new SettingsWindow();
        _settings.Closed += (_, _) => _settings = null;
        _settings.Activate();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        App.Hotkey.Dispose();
        try { TrayIcon.Dispose(); } catch { /* */ }
        Application.Current.Exit();
    }

    private void OnClosed(object sender, WindowEventArgs e)
    {
        App.Hotkey.Dispose();
        try { TrayIcon.Dispose(); } catch { /* */ }
    }
}

internal sealed class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
