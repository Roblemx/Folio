using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Folio.Models;

namespace Folio.Services.Persistence;

public interface IWatchStore
{
    List<WatchedAddress> Load();
    void Save(IEnumerable<WatchedAddress> addresses);
}

/// <summary>Loads/saves watch-only addresses to plaintext <c>watch.json</c>.</summary>
public sealed class WatchStore : IWatchStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _file;

    public WatchStore(string dataDirectory)
    {
        _file = Path.Combine(dataDirectory, "watch.json");
    }

    public List<WatchedAddress> Load()
    {
        foreach (var path in new[] { _file, _file + ".bak" })
        {
            var raw = AtomicFile.TryRead(path);
            if (raw is null || raw.Length == 0)
            {
                continue;
            }

            try
            {
                var list = JsonSerializer.Deserialize<List<WatchedAddress>>(raw, Options);
                if (list != null)
                {
                    return list;
                }
            }
            catch
            {
                // fall through to backup
            }
        }

        return new List<WatchedAddress>();
    }

    public void Save(IEnumerable<WatchedAddress> addresses) =>
        AtomicFile.Write(_file, JsonSerializer.SerializeToUtf8Bytes(addresses.ToList(), Options));
}
