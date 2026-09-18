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
}
