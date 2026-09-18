using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WindowsMediaSwitcher.Models;

namespace WindowsMediaSwitcher.Helpers;

/// <summary>
/// Approximates Apple-style liquid glass: dark smoke tint + strong backdrop blur +
/// 1px top hairline highlight. Avoids milky white Acrylic defaults.
/// Primary blur comes from Window.SystemBackdrop (DesktopAcrylicBackdrop).
/// Tokens bind from AppSettings so settings sliders update live.
/// </summary>
public static class LiquidGlassHelper
{
    // Dark smoke tint base
    private static readonly Color SmokeTint = Color.FromArgb(255, 0x14, 0x16, 0x1C);

    public static void ApplyToBorder(Border border, AppSettings s)
    {
        var opacity = Math.Clamp(s.GlassOpacity / 100.0, 0.04, 0.45);
        var strength = Math.Clamp(s.GlassStrength / 100.0, 0.2, 1.0);
        byte a = (byte)Math.Clamp((int)(opacity * 255 * strength), 8, 120);

        var fill = Color.FromArgb(a, SmokeTint.R, SmokeTint.G, SmokeTint.B);
        border.Background = new SolidColorBrush(fill);
        border.CornerRadius = new CornerRadius(s.GlassRadius);

        border.BorderThickness = new Thickness(1);
        border.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    }

    public static Brush CreateHairlineBrush(AppSettings s)
    {
        var highlight = Math.Clamp(s.GlassHighlight / 100.0, 0.05, 0.8);
        byte a = (byte)Math.Clamp((int)(highlight * 180), 20, 160);
        return new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0, 1),
            GradientStops =
            {
                new GradientStop { Color = Color.FromArgb(a, 255, 255, 255), Offset = 0 },
                new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 0.08 },
                new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1 },
            }
        };
    }

    public static void ApplyRowChrome(Border row, bool isFocused)
    {
        row.CornerRadius = new CornerRadius(13);
        row.Height = 50;
        row.Padding = new Thickness(14, 0, 14, 0);

        if (isFocused)
        {
            row.Background = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));
            row.BorderThickness = new Thickness(1);
            row.BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
        }
        else
        {
            row.Background = new SolidColorBrush(Colors.Transparent);
            row.BorderThickness = new Thickness(0);
            row.BorderBrush = null;
        }
    }

    public static Color AccentCheck => Color.FromArgb(255, 0x0A, 0x84, 0xFF);
    public static Color WhiteText => Color.FromArgb(255, 0xF5, 0xF5, 0xF7);

    /// <summary>
    /// Tint color for DesktopAcrylicController / SystemBackdrop when configured in code.
    /// </summary>
    public static Color BackdropTint(AppSettings s)
    {
        var opacity = Math.Clamp(s.GlassOpacity / 100.0, 0.04, 0.45);
        byte a = (byte)Math.Clamp((int)(opacity * 255), 10, 100);
        // Slight cool smoke (#B4BEDC mixed into dark)
        return Color.FromArgb(a, 0x1A, 0x1E, 0x28);
    }
}
