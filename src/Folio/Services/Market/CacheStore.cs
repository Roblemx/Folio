using System;
using System.IO;
using System.Text.Json;
using Folio.Services.Persistence;

namespace Folio.Services.Market;

/// <summary>A cached value plus the time it was fetched.</summary>
public sealed record CacheEntry<T>(DateTimeOffset FetchedAt, T Value);

/// <summary>
/// A tiny, typed JSON disk cache under <c>{dataDir}/cache</c>. Used for the "last known"
/// prices, the coin catalog and historical series, so the app works offline and within
/// API rate limits.
/// </summary>
public sealed class CacheStore
{
    private static readonly JsonSerializerOptions Options = new();
    private readonly string _dir;

    public CacheStore(string dataDirectory) => _dir = Path.Combine(dataDirectory, "cache");

    public void Write<T>(string key, T value)
    {
        var entry = new CacheEntry<T>(DateTimeOffset.UtcNow, value);
        AtomicFile.Write(PathFor(key), JsonSerializer.SerializeToUtf8Bytes(entry, Options));
    }

    public CacheEntry<T>? Read<T>(string key)
    {
        var raw = AtomicFile.TryRead(PathFor(key)) ?? AtomicFile.TryRead(PathFor(key) + ".bak");
        if (raw is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CacheEntry<T>>(raw, Options);
        }
        catch
        {
            return null;
        }
    }

    private string PathFor(string key) => Path.Combine(_dir, Sanitize(key) + ".json");

    private static string Sanitize(string key)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            key = key.Replace(c, '_');
        }

        return key;
    }
}
