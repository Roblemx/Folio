using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Folio.Converters;

/// <summary>bool (is-up) → Positive / Negative brush from the active theme.</summary>
public sealed class UpBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var up = value is bool b && b;
        return Application.Current.TryFindResource(up ? "PositiveBrush" : "NegativeBrush") ?? Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal static class Truthy
{
    public static bool Of(object? value) => value switch
    {
        bool b => b,
        int i => i != 0,
        string s => !string.IsNullOrEmpty(s),
        null => false,
        _ => true
    };
}

/// <summary>truthy (bool / non-zero int / non-empty string) → Visible / Collapsed.</summary>
public sealed class BoolToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        Truthy.Of(value) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility v && v == Visibility.Visible;
}

/// <summary>truthy → Collapsed / Visible (inverse).</summary>
public sealed class InverseBoolToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        Truthy.Of(value) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility v && v != Visibility.Visible;
}

/// <summary>Selected-range string equals parameter → bool (for range-tab IsChecked).</summary>
public sealed class EqualsBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && b ? parameter ?? Binding.DoNothing : Binding.DoNothing;
}

/// <summary>
/// A 0..1 ratio → star <see cref="GridLength"/>. Use to size two columns proportionally
/// (e.g. a gauge marker): pass <c>ConverterParameter=inv</c> for the complementary share.
/// </summary>
public sealed class RatioToStarConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        var ratio = value is double d ? d : 0d;
        ratio = Math.Clamp(ratio, 0.0001, 0.9999);
        var share = string.Equals(parameter?.ToString(), "inv", StringComparison.OrdinalIgnoreCase)
            ? 1 - ratio
            : ratio;
        return new GridLength(share, GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
