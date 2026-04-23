namespace PanelWidgets.Providers;

public sealed class ClockWidgetHandler(string widgetId)
    : BaseWidgetHandler(widgetId, TimeSpan.FromSeconds(30))
{
    private static readonly string _template = LoadTemplate("ClockCard.json");

    protected override Task<(string Template, string Data)> BuildCardAsync()
    {
        var now  = DateTime.Now;
        var data = Serialize(new
        {
            time = now.ToString("HH:mm"),
            date = now.ToString("dddd, MMMM d"),
        });
        return Task.FromResult((_template, data));
    }
}
