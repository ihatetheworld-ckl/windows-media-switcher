using System.Runtime.InteropServices;
using WindowsMediaSwitcher.Helpers;
using WindowsMediaSwitcher.Models;

namespace WindowsMediaSwitcher.Services;

/// <summary>
/// Global hotkey via RegisterHotKey, with WH_KEYBOARD_LL fallback.
/// RegisterHotKey cannot steal OS-reserved combos (e.g. Win+V clipboard).
/// For those — and as best-effort for Win+Ctrl+V — a low-level hook swallows
/// the key chord and raises HotkeyPressed.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    public const int HotkeyId = 0x4D53; // 'MS'

    private IntPtr _hwnd;
    private bool _registered;
    private bool _hookInstalled;
    private NativeMethods.WndProcDelegate? _wndProc;
    private IntPtr _prevWndProc;
    private GCHandle _wndProcHandle;

    private NativeMethods.LowLevelKeyboardProc? _llProc;
    private IntPtr _llHook;
    private GCHandle _llHandle;

    private AppSettings _target = new();
    private long _lastFireTicks;
    private const int DebounceMs = 350;

    public event EventHandler? HotkeyPressed;

    /// <summary>True when RegisterHotKey succeeded; otherwise LL hook is active.</summary>
    public bool UsedRegisterHotKey => _registered;

    /// <summary>True when WH_KEYBOARD_LL is installed (fallback or always-on swallow).</summary>
    public bool UsedKeyboardHook => _hookInstalled;

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
        UninstallHook();

        _target = settings.Clone();

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

        // Always install LL hook as best-effort:
        // - If RegisterHotKey failed (OS-owned / conflict), hook is the only path.
        // - If it succeeded, hook still swallows the chord so OS UI does not also open,
        //   and debounce prevents double popup toggle.
        InstallHook();

        return _registered || _hookInstalled;
    }

    public void Unregister()
    {
        if (_registered && _hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
    }

    private void InstallHook()
    {
        if (_hookInstalled) return;
        _llProc = LowLevelKeyboardProc;
        _llHandle = GCHandle.Alloc(_llProc);
        // Win10+: for WH_KEYBOARD_LL with dwThreadId=0, hMod may be NULL.
        _llHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _llProc,
            IntPtr.Zero,
            0);
        _hookInstalled = _llHook != IntPtr.Zero;
        if (!_hookInstalled && _llHandle.IsAllocated)
            _llHandle.Free();
    }

    private void UninstallHook()
    {
        if (_llHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_llHook);
            _llHook = IntPtr.Zero;
        }
        _hookInstalled = false;
        if (_llHandle.IsAllocated)
            _llHandle.Free();
        _llProc = null;
    }

    private IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                var info = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                if (MatchesTarget((int)info.vkCode))
                {
                    FireDebounced();
                    // Swallow so OS / other apps do not also handle the combo.
                    return (IntPtr)1;
                }
            }
        }
        return NativeMethods.CallNextHookEx(_llHook, nCode, wParam, lParam);
    }

    private bool MatchesTarget(int vk)
    {
        // Ignore pure modifiers
        if (vk is NativeMethods.VK_LWIN or NativeMethods.VK_RWIN
            or NativeMethods.VK_CONTROL or NativeMethods.VK_LCONTROL or NativeMethods.VK_RCONTROL
            or NativeMethods.VK_MENU or NativeMethods.VK_LMENU or NativeMethods.VK_RMENU
            or NativeMethods.VK_SHIFT or NativeMethods.VK_LSHIFT or NativeMethods.VK_RSHIFT)
            return false;

        if (vk != _target.HotkeyVirtualKey)
            return false;

        bool win = IsDown(NativeMethods.VK_LWIN) || IsDown(NativeMethods.VK_RWIN);
        bool ctrl = IsDown(NativeMethods.VK_CONTROL) || IsDown(NativeMethods.VK_LCONTROL) || IsDown(NativeMethods.VK_RCONTROL);
        bool alt = IsDown(NativeMethods.VK_MENU) || IsDown(NativeMethods.VK_LMENU) || IsDown(NativeMethods.VK_RMENU);
        bool shift = IsDown(NativeMethods.VK_SHIFT) || IsDown(NativeMethods.VK_LSHIFT) || IsDown(NativeMethods.VK_RSHIFT);

        return win == _target.HotkeyWin
            && ctrl == _target.HotkeyCtrl
            && alt == _target.HotkeyAlt
            && shift == _target.HotkeyShift;
    }

    private static bool IsDown(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    private void FireDebounced()
    {
        var now = Environment.TickCount64;
        if (now - _lastFireTicks < DebounceMs) return;
        _lastFireTicks = now;
        HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            FireDebounced();
            return IntPtr.Zero;
        }
        return NativeMethods.CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    private void Detach()
    {
        Unregister();
        UninstallHook();
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
