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
    // Strong reference so SettingsWindow is not GC'd while open
    private SettingsWindow? _settings;
    private bool _hotkeyAttached;
    private bool _startupUpdateScheduled;

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
        if (!_hotkeyAttached)
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            if (hwnd != IntPtr.Zero)
            {
                WindowNativeHelper.HideFromTaskbar(hwnd);
                App.Hotkey.Attach(hwnd);
                ReregisterHotkey();
                _hotkeyAttached = true;
            }
        }

        AppWindow.Hide();

        if (!_startupUpdateScheduled)
        {
            _startupUpdateScheduled = true;
            if (App.Settings.Current.CheckUpdatesOnStartup)
            {
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    _ = CheckUpdatesQuietAsync();
                });
            }
        }
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

    /// <summary>
    /// Tray MenuFlyout closes before a new Window can activate if we open synchronously.
    /// Defer to DispatcherQueue and keep a strong reference; surface errors via tray tip.
    /// </summary>
    private void OnOpenSettings(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, OpenSettingsCore);
    }

    private void OpenSettingsCore()
    {
        try
        {
            if (_settings is not null)
            {
                try { _settings.AppWindow.Show(); } catch { /* */ }
                _settings.Activate();
                var existing = WindowNative.GetWindowHandle(_settings);
                if (existing != IntPtr.Zero)
                {
                    NativeMethods.ShowWindow(existing, NativeMethods.SW_RESTORE);
                    NativeMethods.SetForegroundWindow(existing);
                }
                return;
            }

            _settings = new SettingsWindow();
            _settings.Closed += (_, _) => _settings = null;
            try { _settings.AppWindow.Show(); } catch { /* */ }
            _settings.Activate();

            var hwnd = WindowNative.GetWindowHandle(_settings);
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);
                NativeMethods.SetForegroundWindow(hwnd);
            }
        }
        catch (Exception ex)
        {
            TryTrayNotify("无法打开设置", ex.Message);
        }
    }

    private async Task CheckUpdatesQuietAsync()
    {
        try
        {
            var result = await App.Updates.CheckForUpdatesAsync().ConfigureAwait(true);
            if (result.UpdateAvailable && result.LatestVersion is not null)
            {
                TryTrayNotify(
                    "发现新版本",
                    $"v{result.LatestVersion} 可用（当前 v{result.CurrentVersion}）。请在设置中点击「检查更新」。");
            }
        }
        catch
        {
            // Quiet on startup
        }
    }

    internal void TryTrayNotify(string title, string message)
    {
        try
        {
            // H.NotifyIcon balloon / ShowNotification APIs vary by version — reflect safely
            var trayType = TrayIcon.GetType();
            var showNotif = trayType.GetMethod("ShowNotification",
                new[] { typeof(string), typeof(string) });
            if (showNotif is not null)
            {
                showNotif.Invoke(TrayIcon, new object[] { title, message });
                return;
            }

            // Fallback: ToolTipText flash
            TrayIcon.ToolTipText = $"{title}: {message}";
        }
        catch
        {
            // ignore
        }
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
