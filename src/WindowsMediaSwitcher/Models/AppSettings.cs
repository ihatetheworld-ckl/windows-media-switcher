using System.Text.Json.Serialization;

namespace WindowsMediaSwitcher.Models;

/// <summary>Persisted settings for Windows Media Switcher.</summary>
public sealed class AppSettings
{
    // Hotkey: default Win+Ctrl+V (was Win+Shift+V)
    public bool HotkeyWin { get; set; } = true;
    public bool HotkeyCtrl { get; set; } = true;
    public bool HotkeyAlt { get; set; } = false;
    public bool HotkeyShift { get; set; } = false;
    public int HotkeyVirtualKey { get; set; } = 0x56; // 'V'

    // Liquid Glass tokens
    public double GlassStrength { get; set; } = 80;
    public double GlassBlur { get; set; } = 40;
    public double GlassHighlight { get; set; } = 36;
    public double GlassOpacity { get; set; } = 22; // percent
    public double GlassRadius { get; set; } = 36;

    // Behavior
    public bool CloseOnBlur { get; set; } = true;
    public bool CloseOnSelect { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool CheckUpdatesOnStartup { get; set; } = true;

    [JsonIgnore]
    public string HotkeyDisplay
    {
        get
        {
            var parts = new List<string>();
            if (HotkeyWin) parts.Add("Win");
            if (HotkeyCtrl) parts.Add("Ctrl");
            if (HotkeyAlt) parts.Add("Alt");
            if (HotkeyShift) parts.Add("Shift");
            parts.Add(VkToName(HotkeyVirtualKey));
            return string.Join("+", parts);
        }
    }

    public static string VkToName(int vk) => vk switch
    {
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
        0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
        0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
        0x20 => "Space", 0x09 => "Tab", 0x0D => "Enter",
        0x25 => "Left", 0x26 => "Up", 0x27 => "Right", 0x28 => "Down",
        _ => $"VK_{vk:X2}"
    };

    public AppSettings Clone() => new()
    {
        HotkeyWin = HotkeyWin,
        HotkeyCtrl = HotkeyCtrl,
        HotkeyAlt = HotkeyAlt,
        HotkeyShift = HotkeyShift,
        HotkeyVirtualKey = HotkeyVirtualKey,
        GlassStrength = GlassStrength,
        GlassBlur = GlassBlur,
        GlassHighlight = GlassHighlight,
        GlassOpacity = GlassOpacity,
        GlassRadius = GlassRadius,
        CloseOnBlur = CloseOnBlur,
        CloseOnSelect = CloseOnSelect,
        StartWithWindows = StartWithWindows,
        CheckUpdatesOnStartup = CheckUpdatesOnStartup,
    };
}
