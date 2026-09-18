using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WindowsMediaSwitcher.Helpers;
using WindowsMediaSwitcher.Models;
using Windows.Graphics;

namespace WindowsMediaSwitcher.Views;

public sealed partial class SettingsWindow : Window
{
    private bool _loading = true;
    private bool _recording;
    private AppSettings _draft;
    private string? _pendingDownloadUrl;

    public SettingsWindow()
    {
        InitializeComponent();
        _draft = App.Settings.Current.Clone();

        AppWindow.Resize(new SizeInt32(520, 780));
        Title = "设置 — 媒体输出切换器";

        LoadFromSettings();
        UpdatePreview();
        VersionLabel.Text = $"当前版本 v{App.Updates.CurrentVersion}";
        _loading = false;

        RootHook();
    }

    private void RootHook()
    {
        if (Content is UIElement root)
        {
            root.KeyDown += OnRootKeyDown;
        }
    }

    private void LoadFromSettings()
    {
        HotkeyLabel.Text = _draft.HotkeyDisplay;
        StrengthSlider.Value = _draft.GlassStrength;
        BlurSlider.Value = _draft.GlassBlur;
        HighlightSlider.Value = _draft.GlassHighlight;
        OpacitySlider.Value = _draft.GlassOpacity;
        RadiusSlider.Value = _draft.GlassRadius;
        CloseOnBlurToggle.IsOn = _draft.CloseOnBlur;
        CloseOnSelectToggle.IsOn = _draft.CloseOnSelect;
        AutostartToggle.IsOn = _draft.StartWithWindows;
        CheckUpdatesToggle.IsOn = _draft.CheckUpdatesOnStartup;
    }

    private void Persist()
    {
        if (_loading) return;
        App.Settings.Replace(_draft.Clone());
    }

    private void OnGlassChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading) return;
        _draft.GlassStrength = StrengthSlider.Value;
        _draft.GlassBlur = BlurSlider.Value;
        _draft.GlassHighlight = HighlightSlider.Value;
        _draft.GlassOpacity = OpacitySlider.Value;
        _draft.GlassRadius = RadiusSlider.Value;
        UpdatePreview();
        Persist();
    }

    private void UpdatePreview()
    {
        LiquidGlassHelper.ApplyToBorder(PreviewGlass, _draft);
        PreviewHairline.Background = LiquidGlassHelper.CreateHairlineBrush(_draft);
    }

    private void OnBehaviorChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _draft.CloseOnBlur = CloseOnBlurToggle.IsOn;
        _draft.CloseOnSelect = CloseOnSelectToggle.IsOn;
        Persist();
    }

    private void OnAutostartChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _draft.StartWithWindows = AutostartToggle.IsOn;
        App.Autostart.SetEnabled(_draft.StartWithWindows);
        Persist();
    }

    private void OnUpdatesToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _draft.CheckUpdatesOnStartup = CheckUpdatesToggle.IsOn;
        Persist();
    }

    private async void OnCheckUpdate(object sender, RoutedEventArgs e)
    {
        CheckUpdateBtn.IsEnabled = false;
        UpdateStatus.Text = "正在检查…";
        _pendingDownloadUrl = null;

        try
        {
            var result = await App.Updates.CheckForUpdatesAsync();
            if (result.Error is not null && !result.UpdateAvailable)
            {
                UpdateStatus.Text = $"检查失败：{result.Error}";
                return;
            }

            if (!result.UpdateAvailable)
            {
                UpdateStatus.Text = $"已是最新版本（v{result.CurrentVersion}）";
                return;
            }

            if (string.IsNullOrEmpty(result.DownloadUrl))
            {
                UpdateStatus.Text = result.Error ?? "发现新版本但缺少下载地址";
                return;
            }

            _pendingDownloadUrl = result.DownloadUrl;
            UpdateStatus.Text = $"发现 v{result.LatestVersion}（当前 v{result.CurrentVersion}）。点击下方确认安装。";
            CheckUpdateBtn.Content = "下载并安装更新";
            CheckUpdateBtn.Click -= OnCheckUpdate;
            CheckUpdateBtn.Click += OnInstallUpdate;
        }
        catch (Exception ex)
        {
            UpdateStatus.Text = $"检查失败：{ex.Message}";
        }
        finally
        {
            CheckUpdateBtn.IsEnabled = true;
        }
    }

    private async void OnInstallUpdate(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_pendingDownloadUrl)) return;

        CheckUpdateBtn.IsEnabled = false;
        UpdateStatus.Text = "正在下载并准备更新，应用即将退出…";

        try
        {
            await App.Updates.ApplyUpdateAsync(_pendingDownloadUrl);
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            UpdateStatus.Text = $"更新失败：{ex.Message}";
            CheckUpdateBtn.IsEnabled = true;
        }
    }

    private void OnRecordHotkey(object sender, RoutedEventArgs e)
    {
        _recording = true;
        RecordHint.Text = "请按下组合键（需包含 Win/Ctrl/Alt/Shift 之一 + 主键）… Esc 取消";
        RecordHotkeyBtn.Content = "录制中…";
    }

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_recording) return;

        if (e.Key == VirtualKey.Escape)
        {
            _recording = false;
            RecordHint.Text = "已取消";
            RecordHotkeyBtn.Content = "录制热键…";
            e.Handled = true;
            return;
        }

        if (e.Key is VirtualKey.Control or VirtualKey.Shift or VirtualKey.Menu
            or VirtualKey.LeftWindows or VirtualKey.RightWindows
            or VirtualKey.LeftControl or VirtualKey.RightControl
            or VirtualKey.LeftShift or VirtualKey.RightShift
            or VirtualKey.LeftMenu or VirtualKey.RightMenu)
        {
            return;
        }

        bool win = IsDown(VirtualKey.LeftWindows) || IsDown(VirtualKey.RightWindows);
        bool ctrl = IsDown(VirtualKey.Control) || IsDown(VirtualKey.LeftControl) || IsDown(VirtualKey.RightControl);
        bool alt = IsDown(VirtualKey.Menu) || IsDown(VirtualKey.LeftMenu) || IsDown(VirtualKey.RightMenu);
        bool shift = IsDown(VirtualKey.Shift) || IsDown(VirtualKey.LeftShift) || IsDown(VirtualKey.RightShift);

        if (!win && !ctrl && !alt && !shift)
        {
            RecordHint.Text = "请至少按住一个修饰键（Win / Ctrl / Alt / Shift）";
            return;
        }

        _draft.HotkeyWin = win;
        _draft.HotkeyCtrl = ctrl;
        _draft.HotkeyAlt = alt;
        _draft.HotkeyShift = shift;
        _draft.HotkeyVirtualKey = (int)e.Key;

        HotkeyLabel.Text = _draft.HotkeyDisplay;
        _recording = false;
        RecordHint.Text = "已保存热键";
        RecordHotkeyBtn.Content = "录制热键…";
        Persist();
        e.Handled = true;
    }

    private static bool IsDown(VirtualKey key)
    {
        try
        {
            var s = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key);
            return s.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        }
        catch
        {
            return false;
        }
    }
}
