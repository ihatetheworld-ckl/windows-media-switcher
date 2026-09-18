using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace WindowsMediaSwitcher.Helpers;

public static class WindowNativeHelper
{
    public static IntPtr GetHwnd(Window window) => WindowNative.GetWindowHandle(window);

    public static void ApplyToolWindowTopmost(IntPtr hwnd, bool topmost = true)
    {
        var ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_TOOLWINDOW;
        if (topmost) ex |= NativeMethods.WS_EX_TOPMOST;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, ex);

        if (topmost)
        {
            NativeMethods.SetWindowPos(
                hwnd,
                (IntPtr)NativeMethods.HWND_TOPMOST,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW);
        }
    }

    public static void HideFromTaskbar(IntPtr hwnd)
    {
        var ex = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
        ex |= NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, ex);
    }

    /// <summary>
    /// Remove system white rectangular border and align DWM corner radius with liquid-glass UI.
    /// Win11: DWMWA_BORDER_COLOR=NONE + ROUND corners. Older builds no-op safely.
    /// </summary>
    public static void ApplyBorderlessRoundedChrome(IntPtr hwnd, bool roundCorners = true)
    {
        try
        {
            // Strip caption/thickframe leftovers that can paint a hard white frame
            var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_STYLE);
            style &= ~(NativeMethods.WS_CAPTION | NativeMethods.WS_THICKFRAME | NativeMethods.WS_BORDER);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_STYLE, style);

            int corner = roundCorners ? NativeMethods.DWMWCP_ROUND : NativeMethods.DWMWCP_DONOTROUND;
            _ = NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref corner,
                sizeof(int));

            int none = NativeMethods.DWMWA_COLOR_NONE;
            _ = NativeMethods.DwmSetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_BORDER_COLOR,
                ref none,
                sizeof(int));

            NativeMethods.SetWindowPos(
                hwnd,
                IntPtr.Zero,
                0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
        }
        catch
        {
            // Pre-Win11 / DWM unavailable — ignore
        }
    }
}
