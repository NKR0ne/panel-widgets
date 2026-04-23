using PanelWidgets.Services;

namespace PanelWidgets.Providers;

public sealed class TrafficWidgetHandler(string widgetId)
    : BaseWidgetHandler(widgetId, TimeSpan.FromMinutes(5))
{
    private static readonly string _template = LoadTemplate("TrafficCard.json");

    protected override async Task<(string Template, string Data)> BuildCardAsync()
    {
        var info = await TrafficService.Default.GetCommuteAsync();

        var data = Serialize(new
        {
            origin           = info.Origin,
            destination      = info.Destination,
            travelTime       = info.TravelTime,
            congestionLevel  = info.CongestionLevel,
            congestionColor  = CongestionColor(info.CongestionLevel),
            distance         = info.Distance,
        });
        return (_template, data);
    }

    private static string CongestionColor(string level) => level.ToLowerInvariant() switch
    {
        "low"      => "Good",
        "moderate" => "Warning",
        "high"     => "Attention",
        _          => "Default",
    };
}
