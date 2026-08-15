using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Folio.Services.Persistence;

namespace Folio.Services;

public interface IIconService
{
    /// <summary>The cached coin logo if present (memory or disk), else null.</summary>
    ImageSource? Get(string coinId);

    /// <summary>Ensures a logo is downloaded and cached for later (no-op if already cached or no URL).</summary>
    void Request(string coinId, string? imageUrl);

    /// <summary>Raised (with the coin id) when a newly downloaded logo becomes available.</summary>
    event EventHandler<string>? IconReady;
}

/// <summary>
/// Downloads and disk-caches coin logos from the market API (CoinGecko image URLs), so the UI
/// can show real icons and keep showing them offline. Falls back silently to initials when a
/// logo isn't available. Cached under <c>%AppData%/Folio/icons</c>.
/// </summary>
public sealed class IconService : IIconService
{
    /// <summary>Set at startup so XAML-instantiated <c>CoinIcon</c> controls can reach the cache.</summary>
    public static IIconService? Instance { get; private set; }

    private readonly HttpClient _http;
    private readonly string _dir;
    private readonly ConcurrentDictionary<string, ImageSource> _memory = new();
    private readonly ConcurrentDictionary<string, byte> _inFlight = new();

    public IconService(HttpClient http)
    {
        _http = http;
        _dir = Path.Combine(AppPaths.DataDirectory, "icons");
        Directory.CreateDirectory(_dir);
        Instance = this;
    }

    public event EventHandler<string>? IconReady;

    public ImageSource? Get(string coinId)
    {
        if (string.IsNullOrEmpty(coinId))
        {
            return null;
        }

        if (_memory.TryGetValue(coinId, out var cached))
        {
            return cached;
        }

        var path = PathFor(coinId);
        if (File.Exists(path))
        {
            try
            {
                var image = Load(path);
                _memory[coinId] = image;
                return image;
            }
            catch
            {
                // unreadable/corrupt icon — fall through to no icon
            }
        }

        return null;
    }

    public void Request(string coinId, string? imageUrl)
    {
        if (string.IsNullOrEmpty(coinId) || string.IsNullOrEmpty(imageUrl))
        {
            return;
        }

        if (_memory.ContainsKey(coinId) || File.Exists(PathFor(coinId)))
        {
            return;
        }

        if (!_inFlight.TryAdd(coinId, 0))
        {
            return;
        }

        _ = DownloadAsync(coinId, imageUrl);
    }

    private async Task DownloadAsync(string coinId, string url)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(PathFor(coinId), bytes);
            IconReady?.Invoke(this, coinId);
        }
        catch
        {
            // offline / rate-limited — try again next time it's requested
        }
        finally
        {
            _inFlight.TryRemove(coinId, out _);
        }
    }

    private static ImageSource Load(string path)
    {
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        bmp.DecodePixelWidth = 72;
        bmp.UriSource = new Uri(path);
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private string PathFor(string coinId) => Path.Combine(_dir, Sanitize(coinId) + ".png");

    private static string Sanitize(string key)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            key = key.Replace(c, '_');
        }

        return key;
    }
}
