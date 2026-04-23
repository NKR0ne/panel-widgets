using System.Collections.Concurrent;

namespace PanelWidgets.Services;

/// <summary>
/// Simple in-memory cache with per-entry TTL.
/// Prevents hammering APIs when multiple widget instances share the same feed.
/// </summary>
public sealed class CacheService
{
    public static readonly CacheService Default = new();

    private sealed record Entry(object Value, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, Entry> _cache = new();

    public bool TryGet<T>(string key, out T? value)
    {
        if (_cache.TryGetValue(key, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            value = (T)entry.Value;
            return true;
        }
        value = default;
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan ttl)
        where T : notnull
    {
        _cache[key] = new Entry(value, DateTimeOffset.UtcNow.Add(ttl));
    }

    public async Task<T> GetOrFetchAsync<T>(string key, Func<Task<T>> fetch, TimeSpan ttl)
        where T : notnull
    {
        if (TryGet<T>(key, out var cached) && cached is not null)
            return cached;

        var result = await fetch();
        Set(key, result, ttl);
        return result;
    }
}
