using PanelWidgets.Services;

namespace PanelWidgets.Providers;

public sealed class AgendaWidgetHandler(string widgetId)
    : BaseWidgetHandler(widgetId, TimeSpan.FromMinutes(5))
{
    private static readonly string _template = LoadTemplate("AgendaCard.json");

    protected override async Task<(string Template, string Data)> BuildCardAsync()
    {
        var events = await GraphService.Default.GetTodayEventsAsync();

        var data = Serialize(new
        {
            todayLabel = DateTime.Today.ToString("dddd, MMMM d"),
            events = events.Select(e => new
            {
                time  = e.Time,
                title = e.Title,
            }),
        });
        return (_template, data);
    }
}
