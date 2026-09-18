using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.UI.Composition.SystemBackdrops;
using Windows.Graphics;
using Windows.System;
using WindowsMediaSwitcher.Helpers;
using WindowsMediaSwitcher.Models;
using WinRT.Interop;

namespace WindowsMediaSwitcher.Views;

public sealed partial class DevicePopupWindow : Window
{
    private readonly AppSettings _settings;
    public bool IsOpen { get; private set; }

    public DevicePopupWindow()
    {
        InitializeComponent();
        _settings = App.Settings.Current;

        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = null; // custom acrylic below

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
        catch { /* */ }

        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new SizeInt32(360, 480));

        Activated += OnActivated;
        Closed += (_, _) =>
        {
            IsOpen = false;
            DisposeBackdrop();
        };

        ApplyGlass();
        LoadDevices();
    }

    public void ShowPopup()
    {
        PositionBottomRight();
        var hwnd = WindowNative.GetWindowHandle(this);
        WindowNativeHelper.ApplyToolWindowTopmost(hwnd);

        TrySetSystemBackdrop();
        Activate();
        IsOpen = true;

        // Focus list for Enter key
        DeviceList.Focus(FocusState.Programmatic);
        if (DeviceList.Items.Count > 0)
        {
            var defaultIdx = 0;
            for (int i = 0; i < DeviceList.Items.Count; i++)
            {
                if (DeviceList.Items[i] is AudioDeviceInfo d && d.IsDefault)
                {
                    defaultIdx = i;
                    break;
                }
            }
            DeviceList.SelectedIndex = defaultIdx;
        }
    }

    public void ClosePopup()
    {
        IsOpen = false;
        Close();
    }

    private void ApplyGlass()
    {
        LiquidGlassHelper.ApplyToBorder(GlassHost, _settings);
        Hairline.Background = LiquidGlassHelper.CreateHairlineBrush(_settings);
    }

    private void TrySetSystemBackdrop()
    {
        try
        {
            // Prefer built-in backdrop API (avoids ICompositionSupportsSystemBackdrop WinRT cast issues)
            if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
            }
        }
        catch
        {
            // Fallback: LiquidGlassHelper tint on GlassHost already applied
        }
    }

    private void DisposeBackdrop()
    {
        try { SystemBackdrop = null; } catch { /* ignore */ }
    }


    private void PositionBottomRight()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var display = DisplayArea.GetFromWindowId(id, DisplayAreaFallback.Primary);
            var work = display.WorkArea;

            const int margin = 20;
            const int width = 360;
            // Estimate height from item count
            var rows = Math.Max(1, DeviceList.Items.Count);
            var height = Math.Min(480, 56 + rows * 54);
            height = Math.Max(height, 120);

            AppWindow.Resize(new SizeInt32(width, height));
            var x = work.X + work.Width - width - margin;
            var y = work.Y + work.Height - height - margin;
            AppWindow.Move(new PointInt32(x, y));
        }
        catch
        {
            AppWindow.Move(new PointInt32(100, 100));
        }
    }

    private void LoadDevices()
    {
        var devices = App.Audio.GetPlaybackDevices();
        DeviceList.ItemsSource = devices;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (_backdropConfig is not null)
            _backdropConfig.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;

        if (e.WindowActivationState == WindowActivationState.Deactivated
            && _settings.CloseOnBlur)
        {
            ClosePopup();
        }
    }

    private void OnDeviceClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AudioDeviceInfo device)
            SelectDevice(device);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter || e.Key == VirtualKey.Space)
        {
            if (DeviceList.SelectedItem is AudioDeviceInfo device)
            {
                SelectDevice(device);
                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ClosePopup();
            e.Handled = true;
        }
    }

    private void SelectDevice(AudioDeviceInfo device)
    {
        if (!device.IsDefault)
            App.Audio.SetDefaultPlaybackDevice(device.Id);

        if (_settings.CloseOnSelect)
            ClosePopup();
        else
            LoadDevices();
    }
}
