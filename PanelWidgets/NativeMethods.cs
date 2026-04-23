using System.Runtime.InteropServices;

namespace PanelWidgets;

// Classic COM interfaces needed for out-of-proc widget provider registration.
// The widget host uses CoCreateInstance to activate our provider via the CLSID
// declared in Package.appxmanifest.

[ComImport]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(
        [MarshalAs(UnmanagedType.IUnknown)] object? pUnkOuter,
        ref Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

internal static class NativeMethods
{
    // CLSCTX_LOCAL_SERVER — run in a separate process (our EXE)
    public const uint CLSCTX_LOCAL_SERVER = 0x4;

    // REGCLS_MULTIPLEUSE  — one factory serves all requests
    // REGCLS_SUSPENDED    — don't start accepting activations until CoResumeClassObjects
    public const uint REGCLS_MULTIPLEUSE = 1;
    public const uint REGCLS_SUSPENDED   = 4;

    [DllImport("ole32.dll")]
    public static extern int CoRegisterClassObject(
        ref Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister);

    [DllImport("ole32.dll")]
    public static extern int CoRevokeClassObject(uint dwRegister);

    [DllImport("ole32.dll")]
    public static extern int CoResumeClassObjects();
}
