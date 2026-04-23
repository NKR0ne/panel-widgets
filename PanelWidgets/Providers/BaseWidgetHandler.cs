using Microsoft.Windows.Widgets.Providers;
using System.Reflection;
using System.Text.Json;

namespace PanelWidgets.Providers;

/// <summary>
/// Base class for all widget handlers.  Each handler manages one logical widget type
/// (a single DefinitionId) across any number of live instances (one per user pin).
/// The widget host can create multiple instances of the same definition.
/// </summary>
public abstract class BaseWidgetHandler : IDisposable
{
    public string WidgetId { get; }

    protected bool IsActive { get; private set; }
    protected WidgetContext? Context { get; private set; }

    private Timer? _refreshTimer;
    private readonly TimeSpan _refreshInterval;
    private bool _disposed;

    protected BaseWidgetHandler(string widgetId, TimeSpan refreshInterval)
    {
        WidgetId = widgetId;
        _refreshInterval = refreshInterval;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public virtual void Activate(WidgetContext context)
    {
        Context = context;
        IsActive = true;

        // Push first content immediately, then start the periodic timer.
        _ = PushUpdateAsync();
        _refreshTimer = new Timer(_ => _ = PushUpdateAsync(), null,
            _refreshInterval, _refreshInterval);
    }

    public virtual void Deactivate()
    {
        IsActive = false;
        _refreshTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public virtual void OnContextChanged(WidgetContext context)
    {
        Context = context;
        _ = PushUpdateAsync();
    }

    public virtual void RequestUpdate() => _ = PushUpdateAsync();

    public virtual void OnActionInvoked(WidgetActionInvokedArgs args) { }

    // ── Card content ─────────────────────────────────────────────────────────

    /// <summary>Returns (templateJson, dataJson) for the current widget state.</summary>
    protected abstract Task<(string Template, string Data)> BuildCardAsync();

    private async Task PushUpdateAsync()
    {
        try
        {
            var (template, data) = await BuildCardAsync();
            var options = new WidgetUpdateRequestOptions(WidgetId)
            {
                Template = template,
                Data     = data,
            };
            WidgetManager.GetDefault().UpdateWidget(options);
        }
        catch (Exception ex)
        {
            PushError(ex.Message);
        }
    }

    protected void PushError(string message)
    {
        var options = new WidgetUpdateRequestOptions(WidgetId)
        {
            Template = ErrorCardTemplate,
            Data     = JsonSerializer.Serialize(new { message }),
        };
        WidgetManager.GetDefault().UpdateWidget(options);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Loads an embedded *.json card template by file name (e.g., "RssCard.json").</summary>
    protected static string LoadTemplate(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"PanelWidgets.Cards.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    protected static string Serialize(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    private const string ErrorCardTemplate = """
        {
          "type": "AdaptiveCard",
          "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
          "version": "1.5",
          "body": [
            { "type": "TextBlock", "text": "⚠ ${message}", "wrap": true, "isSubtle": true, "size": "Small" }
          ]
        }
        """;

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer?.Dispose();
        IsActive = false;
        GC.SuppressFinalize(this);
    }
}
