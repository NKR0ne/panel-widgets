using Microsoft.Windows.Widgets.Providers;
using PanelWidgets.Services;

namespace PanelWidgets.Providers;

public sealed class RssWidgetHandler(string widgetId, string feedTitle, string feedUrl)
    : BaseWidgetHandler(widgetId, TimeSpan.FromMinutes(10))
{
    private static readonly string _template = LoadTemplate("RssCard.json");

    protected override async Task<(string Template, string Data)> BuildCardAsync()
    {
        // Large card: 8 articles; medium: 4
        int count = Context?.Size == WidgetSize.Large ? 8 : 4;
        var articles = await RssService.Default.GetArticlesAsync(feedUrl, count);

        var data = Serialize(new
        {
            feedTitle,
            articles = articles.Select(a => new
            {
                headline = a.Title,
                source   = a.Source,
                age      = FormatAge(a.Published),
                url      = a.Url,
            }),
        });
        return (_template, data);
    }

    public override void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        if (args.Verb == "openArticle")
        {
            // Parse the URL from the action data and open in default browser.
            using var doc = System.Text.Json.JsonDocument.Parse(args.Data);
            if (doc.RootElement.TryGetProperty("url", out var urlEl))
            {
                var url = urlEl.GetString();
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    _ = Windows.System.Launcher.LaunchUriAsync(uri);
            }
        }
    }

    private static string FormatAge(DateTimeOffset published)
    {
        var age = DateTimeOffset.UtcNow - published;
        if (age.TotalMinutes < 60)  return $"{(int)age.TotalMinutes}m";
        if (age.TotalHours   < 24)  return $"{(int)age.TotalHours}h";
        return $"{(int)age.TotalDays}d";
    }
}
