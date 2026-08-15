using System.IO;
using System.Text.Json;
using Folio.Models;

namespace Folio.Services.Persistence;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

/// <summary>Loads/saves app settings to plaintext <c>settings.json</c> (never encrypted, so
/// theme and the encrypted-flag are readable before any unlock).</summary>
public sealed class SettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _file;

    public SettingsStore(string dataDirectory)
    {
        _file = Path.Combine(dataDirectory, "settings.json");
    }

    public AppSettings Load()
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
                var settings = JsonSerializer.Deserialize<AppSettings>(raw, Options);
                if (settings != null)
                {
                    return settings;
                }
            }
            catch
            {
                // fall through to backup
            }
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings) =>
        AtomicFile.Write(_file, JsonSerializer.SerializeToUtf8Bytes(settings, Options));
}
