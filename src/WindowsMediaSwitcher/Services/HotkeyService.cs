using System.Runtime.InteropServices;
using WindowsMediaSwitcher.Helpers;
using WindowsMediaSwitcher.Models;

namespace WindowsMediaSwitcher.Services;

/// <summary>
/// Global hotkey via RegisterHotKey / UnregisterHotKey (user32).
/// Requires a message-pump HWND; call Register after window handle is ready.
/// Idle cost is near-zero: OS delivers WM_HOTKEY only when pressed.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    public const int HotkeyId = 0x4D53; // 'MS'

    private IntPtr _hwnd;
    private bool _registered;
    private NativeMethods.WndProcDelegate? _wndProc;
    private IntPtr _prevWndProc;
    // Keep delegate rooted so GC does not collect the thunk while subclassed
    private GCHandle _wndProcHandle;

    public event EventHandler? HotkeyPressed;

    public void Attach(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) throw new ArgumentException("HWND required", nameof(hwnd));
        Detach();
        _hwnd = hwnd;
        _wndProc = WndProc;
        _wndProcHandle = GCHandle.Alloc(_wndProc);
        _prevWndProc = NativeMethods.SetWindowLongPtr(
            _hwnd,
            NativeMethods.GWLP_WNDPROC,
            Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    public bool Register(AppSettings settings)
    {
        if (_hwnd == IntPtr.Zero) return false;
        Unregister();

        uint mods = NativeMethods.MOD_NOREPEAT;
        if (settings.HotkeyWin) mods |= NativeMethods.MOD_WIN;
        if (settings.HotkeyCtrl) mods |= NativeMethods.MOD_CONTROL;
        if (settings.HotkeyAlt) mods |= NativeMethods.MOD_ALT;
        if (settings.HotkeyShift) mods |= NativeMethods.MOD_SHIFT;

        _registered = NativeMethods.RegisterHotKey(
            _hwnd,
            HotkeyId,
            mods,
            (uint)settings.HotkeyVirtualKey);

        return _registered;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            return IntPtr.Zero;
        }
        return NativeMethods.CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    private void Detach()
    {
        Unregister();
        if (_hwnd != IntPtr.Zero && _prevWndProc != IntPtr.Zero)
        {
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GWLP_WNDPROC, _prevWndProc);
            _prevWndProc = IntPtr.Zero;
            _hwnd = IntPtr.Zero;
        }
        if (_wndProcHandle.IsAllocated)
            _wndProcHandle.Free();
        _wndProc = null;
    }

    public void Dispose()
    {
        Detach();
        GC.SuppressFinalize(this);
    }
}
