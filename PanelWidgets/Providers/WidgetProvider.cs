using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;

namespace PanelWidgets.Providers;

/// <summary>
/// Single IWidgetProvider COM object that the Windows Widget Board activates.
/// Dispatches lifecycle calls to per-definition handler instances.
/// </summary>
[ComVisible(true)]
[Guid("6B4A3F5E-7C2D-4E8A-9B1C-0D3F5E7A9C2B")]
public sealed class WidgetProvider : IWidgetProvider, IWidgetProvider2
{
    // All live widget instances keyed by WidgetId (unique per user pin).
    private static readonly ConcurrentDictionary<string, BaseWidgetHandler> _widgets = new();

    // Tracks how many instances are alive; when it hits 0 the COM server can exit.
    private static int _widgetCount;
    private static readonly ManualResetEventSlim _allClosed = new(false);

    // ── Factory ──────────────────────────────────────────────────────────────

    private static BaseWidgetHandler CreateHandler(WidgetContext ctx) => ctx.DefinitionId switch
    {
        "PanelWidgets_Clock"            => new ClockWidgetHandler(ctx.Id),
        "PanelWidgets_Agenda"           => new AgendaWidgetHandler(ctx.Id),
        "PanelWidgets_Todo"             => new TodoWidgetHandler(ctx.Id),
        "PanelWidgets_Traffic"          => new TrafficWidgetHandler(ctx.Id),
        "PanelWidgets_RssInternational" => new RssWidgetHandler(ctx.Id, "International",
                                              "https://www.lapresse.ca/international/rss"),
        "PanelWidgets_RssActualites"    => new RssWidgetHandler(ctx.Id, "Actualités",
                                              "https://www.lapresse.ca/actualites/rss"),
        "PanelWidgets_RssAiNews"        => new RssWidgetHandler(ctx.Id, "AI News",
                                              "https://venturebeat.com/category/ai/feed/"),
        "PanelWidgets_RssTech"          => new RssWidgetHandler(ctx.Id, "Tech",
                                              "https://www.theverge.com/rss/index.xml"),
        _ => throw new ArgumentException($"Unknown widget definition: {ctx.DefinitionId}")
    };

    // ── IWidgetProvider ──────────────────────────────────────────────────────

    public void CreateWidget(WidgetContext ctx)
    {
        var handler = CreateHandler(ctx);
        _widgets[ctx.Id] = handler;
        Interlocked.Increment(ref _widgetCount);
        _allClosed.Reset();
        // Activate is called separately by the host via IWidgetProvider2.Activate.
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        if (_widgets.TryRemove(widgetId, out var handler))
        {
            handler.Dispose();
            if (Interlocked.Decrement(ref _widgetCount) == 0)
                _allClosed.Set();
        }
    }

    public void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        if (_widgets.TryGetValue(args.WidgetContext.Id, out var h))
            h.OnActionInvoked(args);
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs args)
    {
        if (_widgets.TryGetValue(args.WidgetContext.Id, out var h))
            h.OnContextChanged(args.WidgetContext);
    }

    public void UpdateWidget(WidgetUpdateRequestOptions opts)
    {
        if (_widgets.TryGetValue(opts.WidgetId, out var h))
            h.RequestUpdate();
    }

    // ── IWidgetProvider2 (Activate / Deactivate) ─────────────────────────────

    public void Activate(WidgetContext ctx)
    {
        if (_widgets.TryGetValue(ctx.Id, out var h))
            h.Activate(ctx);
    }

    public void Deactivate(string widgetId)
    {
        if (_widgets.TryGetValue(widgetId, out var h))
            h.Deactivate();
    }

    // ── COM server lifecycle ─────────────────────────────────────────────────

    /// <summary>Blocks the COM server thread until every widget has been deleted.</summary>
    public static void WaitForAllWidgetsDeleted() => _allClosed.Wait();
}
