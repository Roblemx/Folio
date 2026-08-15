using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Folio.Models;

namespace Folio.Services.Persistence;

public interface IAlertStore
{
    List<Alert> Load();
    void Save(IEnumerable<Alert> alerts);
}

/// <summary>Loads/saves price alerts to plaintext <c>alerts.json</c> (independent of the portfolio file).</summary>
public sealed class AlertStore : IAlertStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _file;

    public AlertStore(string dataDirectory)
    {
        _file = Path.Combine(dataDirectory, "alerts.json");
    }

    public List<Alert> Load()
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
                var alerts = JsonSerializer.Deserialize<List<Alert>>(raw, Options);
                if (alerts != null)
                {
                    return alerts;
                }
            }
            catch
            {
                // fall through to backup
            }
        }

        return new List<Alert>();
    }

    public void Save(IEnumerable<Alert> alerts) =>
        AtomicFile.Write(_file, JsonSerializer.SerializeToUtf8Bytes(alerts.ToList(), Options));
}
