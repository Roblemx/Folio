using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Folio.Services;

namespace Folio.Controls;

/// <summary>
/// Shows a coin's real logo (downloaded + cached by <see cref="IconService"/>), falling back to
/// a coloured initials circle while it loads or when no logo is available.
/// </summary>
public partial class CoinIcon : UserControl
{
    public static readonly DependencyProperty CoinIdProperty = DependencyProperty.Register(
        nameof(CoinId), typeof(string), typeof(CoinIcon),
        new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty ImageUrlProperty = DependencyProperty.Register(
        nameof(ImageUrl), typeof(string), typeof(CoinIcon),
        new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty InitialProperty = DependencyProperty.Register(
        nameof(Initial), typeof(string), typeof(CoinIcon),
        new PropertyMetadata("?", OnVisualChanged));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(CoinIcon),
        new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(
        nameof(Diameter), typeof(double), typeof(CoinIcon),
        new PropertyMetadata(32.0, OnVisualChanged));

    public CoinIcon()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string? CoinId { get => (string?)GetValue(CoinIdProperty); set => SetValue(CoinIdProperty, value); }
    public string? ImageUrl { get => (string?)GetValue(ImageUrlProperty); set => SetValue(ImageUrlProperty, value); }
    public string? Initial { get => (string?)GetValue(InitialProperty); set => SetValue(InitialProperty, value); }
    public Brush? Accent { get => (Brush?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((CoinIcon)d).Refresh();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (IconService.Instance is { } svc)
        {
            svc.IconReady += OnIconReady;
        }

        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (IconService.Instance is { } svc)
        {
            svc.IconReady -= OnIconReady;
        }
    }

    private void OnIconReady(object? sender, string coinId)
    {
        if (coinId == CoinId)
        {
            Dispatcher.Invoke(Refresh);
        }
    }

    private void Refresh()
    {
        var d = Diameter;
        Root.Width = d;
        Root.Height = d;
        Fallback.Width = d;
        Fallback.Height = d;
        Fallback.CornerRadius = new CornerRadius(d / 2);
        ImageHost.Width = d;
        ImageHost.Height = d;
        InitialText.Text = Initial;
        InitialText.FontSize = Math.Max(8, d * 0.36);
        if (Accent != null)
        {
            Fallback.Background = Accent;
        }

        var image = IconService.Instance?.Get(CoinId ?? string.Empty);
        if (image != null)
        {
            Brush.ImageSource = image;
            ImageHost.Visibility = Visibility.Visible;
            Fallback.Visibility = Visibility.Collapsed;
        }
        else
        {
            ImageHost.Visibility = Visibility.Collapsed;
            Fallback.Visibility = Visibility.Visible;
            IconService.Instance?.Request(CoinId ?? string.Empty, ImageUrl);
        }
    }
}
