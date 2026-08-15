using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Folio.Controls;

/// <summary>
/// A filled area/line chart with an interactive hover crosshair and value tooltip — drawn
/// directly (no charting dependency). Colors resolve from the active theme.
/// </summary>
public sealed class AreaChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable), typeof(AreaChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(AreaChart),
        new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TooltipFormatProperty = DependencyProperty.Register(
        nameof(TooltipFormat), typeof(string), typeof(AreaChart),
        new FrameworkPropertyMetadata("N2"));

    private const double Pad = 2;
    private List<double> _points = new();
    private int _hover = -1;

    public IEnumerable? Values
    {
        get => (IEnumerable?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public string TooltipFormat
    {
        get => (string)GetValue(TooltipFormatProperty);
        set => SetValue(TooltipFormatProperty, value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_points.Count < 2)
        {
            return;
        }

        var stepX = (ActualWidth - (Pad * 2)) / (_points.Count - 1);
        if (stepX <= 0)
        {
            return;
        }

        var idx = Math.Clamp((int)Math.Round((e.GetPosition(this).X - Pad) / stepX), 0, _points.Count - 1);
        if (idx != _hover)
        {
            _hover = idx;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        if (_hover != -1)
        {
            _hover = -1;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        // Transparent fill so the whole surface is hit-testable for hover.
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));

        _points = Values?.Cast<object>().Select(v => (double)Convert.ToDecimal(v)).ToList() ?? new List<double>();
        if (_points.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var min = _points.Min();
        var max = _points.Max();
        var range = max - min;
        if (range <= 0)
        {
            range = 1;
        }

        var w = ActualWidth - (Pad * 2);
        var h = ActualHeight - (Pad * 2);
        var stepX = w / (_points.Count - 1);

        Point At(int i) => new(Pad + (i * stepX), Pad + (h - ((_points[i] - min) / range * h)));

        var line = new StreamGeometry();
        var fill = new StreamGeometry();
        using (var lc = line.Open())
        using (var fc = fill.Open())
        {
            var p0 = At(0);
            lc.BeginFigure(p0, false, false);
            fc.BeginFigure(new Point(Pad, ActualHeight), true, true);
            fc.LineTo(p0, true, true);
            for (var i = 1; i < _points.Count; i++)
            {
                var p = At(i);
                lc.LineTo(p, true, true);
                fc.LineTo(p, true, true);
            }

            fc.LineTo(new Point(Pad + ((_points.Count - 1) * stepX), ActualHeight), true, true);
        }

        line.Freeze();
        fill.Freeze();

        var baseColor = (Stroke as SolidColorBrush)?.Color ?? Colors.DodgerBlue;
        var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        grad.GradientStops.Add(new GradientStop(Color.FromArgb(70, baseColor.R, baseColor.G, baseColor.B), 0));
        grad.GradientStops.Add(new GradientStop(Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 1));
        grad.Freeze();

        dc.DrawGeometry(grad, null, fill);
        dc.DrawGeometry(null, new Pen(Stroke, 1.7) { LineJoin = PenLineJoin.Round }, line);

        if (_hover >= 0 && _hover < _points.Count)
        {
            DrawHover(dc, At(_hover));
        }
    }

    private void DrawHover(DrawingContext dc, Point hp)
    {
        var border = Resource("BorderBrush", Brushes.Gray);
        dc.DrawLine(new Pen(border, 1), new Point(hp.X, Pad), new Point(hp.X, ActualHeight - Pad));
        dc.DrawEllipse(Stroke, null, hp, 3.5, 3.5);

        var text = _points[_hover].ToString(TooltipFormat, CultureInfo.CurrentCulture);
        var ft = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, Resource("TextPrimaryBrush", Brushes.White),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var boxW = ft.Width + 16;
        var boxH = ft.Height + 10;
        var bx = Math.Clamp(hp.X + 10, 0, Math.Max(0, ActualWidth - boxW));
        var by = Math.Clamp(hp.Y - boxH - 8, 0, Math.Max(0, ActualHeight - boxH));
        var rect = new Rect(bx, by, boxW, boxH);

        dc.DrawRoundedRectangle(Resource("SurfaceAltBrush", Brushes.Black), new Pen(border, 1), rect, 7, 7);
        dc.DrawText(ft, new Point(bx + 8, by + 5));
    }

    private Brush Resource(string key, Brush fallback) => TryFindResource(key) as Brush ?? fallback;
}
