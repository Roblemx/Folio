using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Folio.Controls;

/// <summary>One ring segment: a value and its color.</summary>
public sealed class DonutSegment
{
    public double Value { get; set; }
    public Brush Color { get; set; } = Brushes.Gray;
}

/// <summary>A donut/ring chart: one arc per segment, sized by value.</summary>
public sealed class DonutChart : FrameworkElement
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
        nameof(Segments), typeof(IEnumerable), typeof(DonutChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty = DependencyProperty.Register(
        nameof(Thickness), typeof(double), typeof(DonutChart),
        new FrameworkPropertyMetadata(20.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable? Segments
    {
        get => (IEnumerable?)GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    public double Thickness
    {
        get => (double)GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Segments is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        var segments = new List<DonutSegment>();
        double total = 0;
        foreach (var obj in Segments)
        {
            if (obj is DonutSegment s && s.Value > 0)
            {
                segments.Add(s);
                total += s.Value;
            }
        }

        if (total <= 0)
        {
            return;
        }

        var cx = ActualWidth / 2;
        var cy = ActualHeight / 2;
        var radius = (Math.Min(ActualWidth, ActualHeight) - Thickness) / 2;
        if (radius <= 0)
        {
            return;
        }

        const double gap = 1.5; // degrees between segments
        double angle = 0;

        if (segments.Count == 1)
        {
            var pen = MakePen(segments[0].Color);
            dc.DrawEllipse(null, pen, new Point(cx, cy), radius, radius);
            return;
        }

        foreach (var seg in segments)
        {
            var sweep = (seg.Value / total * 360.0) - gap;
            if (sweep <= 0)
            {
                angle += seg.Value / total * 360.0;
                continue;
            }

            var start = Point(cx, cy, radius, angle + (gap / 2));
            var end = Point(cx, cy, radius, angle + (gap / 2) + sweep);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, false, false);
                ctx.ArcTo(end, new Size(radius, radius), 0, sweep > 180,
                    SweepDirection.Clockwise, true, false);
            }

            geo.Freeze();
            dc.DrawGeometry(null, MakePen(seg.Color), geo);
            angle += seg.Value / total * 360.0;
        }
    }

    private Pen MakePen(Brush brush) => new(brush, Thickness)
    {
        StartLineCap = PenLineCap.Flat,
        EndLineCap = PenLineCap.Flat
    };

    private static Point Point(double cx, double cy, double r, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        return new Point(cx + (r * Math.Sin(rad)), cy - (r * Math.Cos(rad)));
    }
}
