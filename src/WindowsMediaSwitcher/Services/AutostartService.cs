using Microsoft.Win32;

namespace WindowsMediaSwitcher.Services;

/// <summary>
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run for unpackaged apps.
/// No elevation required (current-user only).
/// </summary>
public sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowsMediaSwitcher";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var exe = Environment.ProcessPath
                    ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                    ?? string.Empty;
                if (string.IsNullOrEmpty(exe)) return;
                // Quote path; --autostart is reserved for future silent start flags
                key.SetValue(ValueName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry write failure — leave setting as-is
        }
    }
}
