using System;
using System.Collections;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Folio.Controls;

/// <summary>A minimal line chart that plots a sequence of decimals to fill its bounds.</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IEnumerable), typeof(Sparkline),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness), typeof(double), typeof(Sparkline),
        new FrameworkPropertyMetadata(1.6, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Values is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var points = Values.Cast<object>().Select(v => (double)Convert.ToDecimal(v)).ToList();
        if (points.Count < 2)
        {
            return;
        }

        var min = points.Min();
        var max = points.Max();
        var range = max - min;
        if (range <= 0)
        {
            range = 1;
        }

        var pad = StrokeThickness;
        var w = ActualWidth - (pad * 2);
        var h = ActualHeight - (pad * 2);
        var stepX = w / (points.Count - 1);

        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            for (var i = 0; i < points.Count; i++)
            {
                var p = new Point(pad + (i * stepX), pad + (h - ((points[i] - min) / range * h)));
                if (i == 0)
                {
                    ctx.BeginFigure(p, false, false);
                }
                else
                {
                    ctx.LineTo(p, true, true);
                }
            }
        }

        geo.Freeze();
        var pen = new Pen(Stroke, StrokeThickness)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        dc.DrawGeometry(null, pen, geo);
    }
}
