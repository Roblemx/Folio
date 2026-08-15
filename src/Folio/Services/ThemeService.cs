using System;
using System.Windows;
using Microsoft.Win32;

namespace Folio.Services;

public interface IThemeService
{
    /// <summary>The requested theme: "Dark", "Light" or "System".</summary>
    string Theme { get; }

    /// <summary>The effective theme actually applied ("Dark" or "Light").</summary>
    string Effective { get; }

    void Apply(string theme);
}

/// <summary>
/// Swaps the active color resource dictionary at runtime. Because all visuals use
/// <c>DynamicResource</c> for colors, switching updates the whole UI live. "System" follows
/// the Windows app theme.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private const string Sentinel = "BgColor"; // present only in a color dictionary

    public string Theme { get; private set; } = "Dark";

    public string Effective { get; private set; } = "Dark";

    public void Apply(string theme)
    {
        Theme = theme;
        Effective = Resolve(theme);

        var uri = Effective == "Light" ? "Themes/Colors.Light.xaml" : "Themes/Colors.Dark.xaml";
        var dict = new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) };

        var merged = Application.Current.Resources.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            if (merged[i].Contains(Sentinel))
            {
                merged.RemoveAt(i);
            }
        }

        merged.Insert(0, dict);
    }

    private static string Resolve(string theme) =>
        theme == "System" ? DetectSystem() : (theme == "Light" ? "Light" : "Dark");

    private static string DetectSystem()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0 ? "Dark" : "Light";
            }
        }
        catch
        {
            // ignore — fall back to Dark
        }

        return "Dark";
    }
}
