using System.ServiceModel.Syndication;
using System.Xml;

namespace PanelWidgets.Services;

public sealed record RssArticle(string Title, string Source, string Url, DateTimeOffset Published);

public sealed class RssService
{
    public static readonly RssService Default = new();

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task<List<RssArticle>> GetArticlesAsync(string feedUrl, int maxCount = 8)
    {
        var cacheKey = $"rss:{feedUrl}:{maxCount}";
        return await CacheService.Default.GetOrFetchAsync(cacheKey,
            () => FetchAsync(feedUrl, maxCount),
            TimeSpan.FromMinutes(10));
    }

    private async Task<List<RssArticle>> FetchAsync(string feedUrl, int maxCount)
    {
        using var stream = await _http.GetStreamAsync(feedUrl);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
        var feed    = SyndicationFeed.Load(reader);
        var source  = feed.Title?.Text ?? new Uri(feedUrl).Host;

        return feed.Items
            .OrderByDescending(i => i.PublishDate)
            .Take(maxCount)
            .Select(i => new RssArticle(
                Title:     i.Title?.Text ?? "(no title)",
                Source:    source,
                Url:       i.Links.FirstOrDefault()?.Uri.ToString() ?? feedUrl,
                Published: i.PublishDate))
            .ToList();
    }
}
