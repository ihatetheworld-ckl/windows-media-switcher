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

    public SettingsWindow()
    {
        InitializeComponent();
        _draft = App.Settings.Current.Clone();

        AppWindow.Resize(new SizeInt32(520, 720));
        Title = "设置 — 媒体输出切换器";

        LoadFromSettings();
        UpdatePreview();
        _loading = false;

        // Capture keys while recording
        RootHook();
    }

    private void RootHook()
    {
        // Content root key events for hotkey recorder
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
    }

    private void Persist()
    {
        if (_loading) return;
        App.Settings.Replace(_draft.Clone());
        // Hotkey re-register happens via SettingsChanged on MainWindow
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

        // Ignore pure modifiers
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
