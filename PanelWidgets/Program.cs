using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using PanelWidgets.Providers;

namespace PanelWidgets;

class Program
{
    // Required by WinUI 3 / Windows App SDK bootstrap
    [DllImport("Microsoft.ui.xaml.dll")]
    private static extern void XamlCheckProcessRequirements();

    [MTAThread]
    static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        XamlCheckProcessRequirements();

        // The widget host launches us with -Embedding when it needs a provider instance.
        // Any other launch (e.g., user double-clicking the EXE) shows the settings window.
        if (args.Contains("-Embedding"))
        {
            RunAsComServer();
        }
        else
        {
            Microsoft.UI.Xaml.Application.Start(p =>
            {
                var ctx = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(ctx);
                _ = new App();
            });
        }
    }

    static void RunAsComServer()
    {
        var provider = new WidgetProvider();
        var factory  = new WidgetProviderFactory(provider);

        // Register the COM class factory so the widget host can call CreateInstance.
        var clsid = Guid.Parse("6B4A3F5E-7C2D-4E8A-9B1C-0D3F5E7A9C2B");
        var hr    = NativeMethods.CoRegisterClassObject(
            ref clsid,
            factory,
            NativeMethods.CLSCTX_LOCAL_SERVER,
            NativeMethods.REGCLS_MULTIPLEUSE | NativeMethods.REGCLS_SUSPENDED,
            out uint cookie);

        Marshal.ThrowExceptionForHR(hr);
        Marshal.ThrowExceptionForHR(NativeMethods.CoResumeClassObjects());

        // Block until every widget instance created by the host has been deleted.
        // The widget host will release us (and exit the process) when idle.
        WidgetProvider.WaitForAllWidgetsDeleted();

        NativeMethods.CoRevokeClassObject(cookie);
    }
}

// ---------------------------------------------------------------------------
// COM class factory — one shared WidgetProvider instance per process.
// ---------------------------------------------------------------------------
[ComVisible(true)]
internal sealed class WidgetProviderFactory(WidgetProvider provider) : IClassFactory
{
    public int CreateInstance(object? pUnkOuter, ref Guid riid, out object ppvObject)
    {
        ppvObject = provider;
        return 0; // S_OK
    }

    public int LockServer(bool fLock) => 0; // S_OK
}
