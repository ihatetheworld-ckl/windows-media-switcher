using System.Runtime.InteropServices;
using WindowsMediaSwitcher.Models;

namespace WindowsMediaSwitcher.Services;

/// <summary>
/// Enumerates eRender endpoints via IMMDeviceEnumerator (CoreAudio / MMDevice)
/// and sets the system default playback device via undocumented IPolicyConfig.
///
/// LIMITATION: IPolicyConfig is an undocumented Windows COM interface used by
/// sndvol / Sound control panel. Signature may change across Windows builds.
/// No elevation required for the current-user default endpoint switch in practice,
/// but corporate Group Policy may block default-device changes.
/// </summary>
public sealed class AudioDeviceService
{
    private static readonly Guid ImmDeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid PolicyConfigClientClsid = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");

    // DEVICE_STATE_ACTIVE
    private const uint DeviceStateActive = 0x00000001;
    private const int EDataFlowRender = 0; // eRender
    private const int ERoleConsole = 0;
    private const int ERoleMultimedia = 1;
    private const int ERoleCommunications = 2;

    public IReadOnlyList<AudioDeviceInfo> GetPlaybackDevices()
    {
        var list = new List<AudioDeviceInfo>();
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        IMMDevice? defaultDevice = null;

        try
        {
            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(
                Type.GetTypeFromCLSID(ImmDeviceEnumeratorClsid)!)!;

            string? defaultId = null;
            try
            {
                enumerator.GetDefaultAudioEndpoint(EDataFlowRender, ERoleMultimedia, out defaultDevice);
                defaultDevice?.GetId(out defaultId);
            }
            catch
            {
                // no default endpoint
            }

            enumerator.EnumAudioEndpoints(EDataFlowRender, DeviceStateActive, out collection);
            collection.GetCount(out var count);

            for (uint i = 0; i < count; i++)
            {
                collection.Item(i, out var device);
                try
                {
                    device.GetId(out var id);
                    var name = GetFriendlyName(device) ?? id ?? $"Device {i}";
                    list.Add(new AudioDeviceInfo
                    {
                        Id = id ?? string.Empty,
                        Name = name,
                        IsDefault = !string.IsNullOrEmpty(defaultId) &&
                                    string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase),
                        IsActive = true,
                    });
                }
                finally
                {
                    if (device is not null)
                        Marshal.ReleaseComObject(device);
                }
            }
        }
        finally
        {
            if (defaultDevice is not null) Marshal.ReleaseComObject(defaultDevice);
            if (collection is not null) Marshal.ReleaseComObject(collection);
            if (enumerator is not null) Marshal.ReleaseComObject(enumerator);
        }

        return list;
    }

    public bool SetDefaultPlaybackDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId)) return false;

        object? client = null;
        try
        {
            // Undocumented PolicyConfigClient — same pattern as SoundSwitch / AudioSwitcher
            client = Activator.CreateInstance(Type.GetTypeFromCLSID(PolicyConfigClientClsid)!)!;

            try
            {
                var policy = (IPolicyConfig)client;
                policy.SetDefaultEndpoint(deviceId, ERoleConsole);
                policy.SetDefaultEndpoint(deviceId, ERoleMultimedia);
                policy.SetDefaultEndpoint(deviceId, ERoleCommunications);
                return true;
            }
            catch (InvalidCastException)
            {
                // Vista-era vtable layout fallback
                var vista = (IPolicyConfigVista)client;
                vista.SetDefaultEndpoint(deviceId, ERoleConsole);
                vista.SetDefaultEndpoint(deviceId, ERoleMultimedia);
                vista.SetDefaultEndpoint(deviceId, ERoleCommunications);
                return true;
            }
        }
        catch (COMException)
        {
            return false;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (client is not null)
                Marshal.ReleaseComObject(client);
        }
    }

    private static string? GetFriendlyName(IMMDevice device)
    {
        try
        {
            device.OpenPropertyStore(0 /*STGM_READ*/, out var store);
            try
            {
                var key = PkeyDeviceFriendlyName;
                store.GetValue(ref key, out var pv);
                try
                {
                    if (pv.vt == 31 /*VT_LPWSTR*/ && pv.p != IntPtr.Zero)
                        return Marshal.PtrToStringUni(pv.p);
                    if (pv.vt == 8 /*VT_BSTR*/ && pv.p != IntPtr.Zero)
                        return Marshal.PtrToStringBSTR(pv.p);
                }
                finally
                {
                    PropVariantClear(ref pv);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(store);
            }
        }
        catch
        {
            // fall through
        }
        return null;
    }

    // {a45c254e-df1c-4efd-8020-67d146a850e0}, 14
    private static PROPERTYKEY PkeyDeviceFriendlyName = new()
    {
        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
        pid = 14,
    };

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PROPVARIANT pvar);

    #region COM interop

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // PROPVARIANT: on x64 the union payload begins at offset 8
    [StructLayout(LayoutKind.Explicit)]
    private struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr p;
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, uint dwStateMask, out IMMDeviceCollection ppDevices);
        void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        void RegisterEndpointNotificationCallback(IntPtr pClient);
        void UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        void GetCount(out uint pcDevices);
        void Item(uint nDevice, out IMMDevice ppDevice);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        void OpenPropertyStore(uint stgmAccess, out IPropertyStore ppProperties);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        void GetState(out uint pdwState);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        void Commit();
    }

    /// <summary>
    /// Undocumented IPolicyConfig (Windows 7+). Vtable layout mirrors open-source
    /// PolicyConfigClient used by SoundSwitch / EarTrumpet-style tools.
    /// Only SetDefaultEndpoint is invoked; other slots are stubs for layout.
    /// </summary>
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        void GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);
        void GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr ppFormat);
        void ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        void SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        void GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        void SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
        void GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
        void SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        void GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PROPERTYKEY key, ref PROPVARIANT pv);
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int role);
        void SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
    }

    /// <summary>
    /// IPolicyConfigVista — alternate undocumented layout used on some builds.
    /// GUID: 568B9108-44BF-40B4-9006-86AFE5B5A620
    /// </summary>
    [ComImport, Guid("568B9108-44BF-40B4-9006-86AFE5B5A620"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfigVista
    {
        void GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr ppFormat);
        void GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr ppFormat);
        void SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr mixFormat);
        void GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        void SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pmftPeriod);
        void GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pMode);
        void SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        void GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PROPERTYKEY key, out PROPVARIANT pv);
        void SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref PROPERTYKEY key, ref PROPVARIANT pv);
        void SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int role);
        void SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
    }

    #endregion
}
