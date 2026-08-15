using System;
using System.Globalization;
using System.Windows.Media;

namespace Folio.Helpers;

/// <summary>Display formatting for money, amounts, percentages and per-coin accent colors.</summary>
public static class Format
{
    private static readonly string[] Palette =
    {
        "#4F8CFF", "#2FBF71", "#F2B33D", "#B06BFF", "#FF7A59", "#19C3D1",
        "#F0616D", "#8E97A8", "#5B8DEF", "#43C59E", "#E8A33D", "#9B6BFF"
    };

    public static string Fiat(decimal usd, string symbol, decimal rate) =>
        symbol + (usd * rate).ToString("N2", CultureInfo.InvariantCulture);

    public static string Price(decimal usd, string symbol, decimal rate)
    {
        var v = usd * rate;
        var n = Math.Abs(v) < 1m && v != 0m
            ? v.ToString("0.######", CultureInfo.InvariantCulture)
            : v.ToString("N2", CultureInfo.InvariantCulture);
        return symbol + n;
    }

    public static string Amount(decimal a) => a.ToString("0.########", CultureInfo.InvariantCulture);

    /// <summary>Denominate a USD value in BTC (or sats when small). Empty if no BTC price.</summary>
    public static string Btc(decimal usdValue, decimal btcPriceUsd)
    {
        if (btcPriceUsd <= 0m)
        {
            return string.Empty;
        }

        var btc = usdValue / btcPriceUsd;
        return btc >= 0.01m
            ? "₿" + btc.ToString("0.####", CultureInfo.InvariantCulture)
            : (btc * 100_000_000m).ToString("N0", CultureInfo.InvariantCulture) + " sats";
    }

    public static string Percent(decimal p) =>
        (p >= 0 ? "+" : string.Empty) + p.ToString("0.00", CultureInfo.InvariantCulture) + "%";

    public static string MarketCap(decimal usd, string symbol, decimal rate)
    {
        var v = usd * rate;
        return v >= 1_000_000_000m
            ? symbol + (v / 1_000_000_000m).ToString("N2", CultureInfo.InvariantCulture) + "B"
            : symbol + (v / 1_000_000m).ToString("N1", CultureInfo.InvariantCulture) + "M";
    }

    public static Brush AccentFor(string id)
    {
        var index = (int)((uint)StableHash(id) % (uint)Palette.Length);
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(Palette[index]));
    }

    public static string Ago(DateTimeOffset when)
    {
        var span = DateTimeOffset.Now - when;
        if (span < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (span < TimeSpan.FromHours(1))
        {
            return $"{(int)span.TotalMinutes}m ago";
        }

        if (span < TimeSpan.FromDays(1))
        {
            return $"{(int)span.TotalHours}h ago";
        }

        return $"{(int)span.TotalDays}d ago";
    }

    public static string Initial(string symbolOrId)
    {
        var s = (symbolOrId ?? string.Empty).Trim();
        return s.Length == 0 ? "?" : s.Substring(0, Math.Min(2, s.Length)).ToUpperInvariant();
    }

    private static int StableHash(string s)
    {
        unchecked
        {
            var h = 23;
            foreach (var c in s)
            {
                h = (h * 31) + c;
            }

            return h;
        }
    }
}
