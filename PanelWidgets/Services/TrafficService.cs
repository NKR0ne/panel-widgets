using System.Text.Json;
using Windows.Storage;

namespace PanelWidgets.Services;

public sealed record TrafficInfo(
    string Origin,
    string Destination,
    string TravelTime,
    string CongestionLevel,
    string Distance);

public sealed class TrafficService
{
    public static readonly TrafficService Default = new();

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // TomTom Routing API — key stored in LocalSettings under "TomTomApiKey".
    // Origin / Destination stored as "TrafficOrigin" / "TrafficDestination" (lat,lon strings).
    private const string BaseUrl = "https://api.tomtom.com/routing/1/calculateRoute";

    public async Task<TrafficInfo> GetCommuteAsync()
    {
        return await CacheService.Default.GetOrFetchAsync(
            "traffic:commute",
            FetchCommuteAsync,
            TimeSpan.FromMinutes(5));
    }

    private async Task<TrafficInfo> FetchCommuteAsync()
    {
        var settings    = ApplicationData.Current.LocalSettings.Values;
        var apiKey      = settings["TomTomApiKey"]      as string ?? "";
        var origin      = settings["TrafficOrigin"]     as string ?? "";
        var destination = settings["TrafficDestination"]as string ?? "";
        var originLabel = settings["TrafficOriginLabel"]      as string ?? "Home";
        var destLabel   = settings["TrafficDestLabel"]        as string ?? "Work";

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(origin) || string.IsNullOrEmpty(destination))
            return new TrafficInfo(originLabel, destLabel, "—", "Unknown", "—");

        var url = $"{BaseUrl}/{origin}:{destination}/json" +
                  $"?key={apiKey}&traffic=true&travelMode=car&routeType=fastest";

        var json    = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);

        var summary = doc.RootElement
            .GetProperty("routes")[0]
            .GetProperty("summary");

        var seconds         = summary.GetProperty("travelTimeInSeconds").GetInt32();
        var lengthMeters    = summary.GetProperty("lengthInMeters").GetInt32();
        var trafficSeconds  = summary.GetProperty("trafficDelayInSeconds").GetInt32();

        var travelTime       = FormatDuration(seconds);
        var distance         = $"{lengthMeters / 1000.0:0.#} km";
        var congestionLevel  = trafficSeconds < 120 ? "Low"
                             : trafficSeconds < 600 ? "Moderate"
                             : "High";

        return new TrafficInfo(originLabel, destLabel, travelTime, congestionLevel, distance);
    }

    private static string FormatDuration(int seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours}h {ts.Minutes:D2}m"
            : $"{ts.Minutes} min";
    }
}
