using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Track.Core.Models;
using Track.Core.Pace;

namespace Track.Controls;

public sealed class PaceChartControl : Canvas
{
    public static readonly DependencyProperty MeterProperty =
        DependencyProperty.Register(
            nameof(Meter),
            typeof(UsageMeter),
            typeof(PaceChartControl),
            new PropertyMetadata(null, (_, __) => ((PaceChartControl)_).Redraw()));

    public UsageMeter? Meter
    {
        get => (UsageMeter?)GetValue(MeterProperty);
        set => SetValue(MeterProperty, value);
    }

    public PaceChartControl()
    {
        Height = 140;
        ClipToBounds = true;
        SizeChanged += (_, _) => Redraw();
    }

    private void Redraw()
    {
        Children.Clear();
        if (Meter is null || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var pace = PaceCalculator.Calculate(Meter);
        const double padL = 28, padR = 10, padT = 12, padB = 22;
        var w = Math.Max(10, ActualWidth - padL - padR);
        var h = Math.Max(10, ActualHeight - padT - padB);

        // Grid lines at 0/50/100
        foreach (var pct in new[] { 0.0, 50.0, 100.0 })
        {
            var y = padT + h * (1 - pct / 100.0);
            Children.Add(new Line
            {
                X1 = padL,
                X2 = padL + w,
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(0xE7, 0xE5, 0xE4)),
                StrokeThickness = 1
            });
            Children.Add(new TextBlock
            {
                Text = $"{pct:0}",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x71, 0x6C)),
                Margin = new Thickness(2, y - 7, 0, 0)
            });
        }

        // Recommended diagonal
        var recStart = new Point(padL, padT + h);
        var recEnd = new Point(padL + w, padT);
        Children.Add(new Line
        {
            X1 = recStart.X,
            Y1 = recStart.Y,
            X2 = recEnd.X,
            Y2 = recEnd.Y,
            Stroke = new SolidColorBrush(Color.FromRgb(0xA8, 0xA2, 0x9E)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 }
        });

        // Actual cumulative — series if present, else line from 0 to current at elapsed fraction
        var actualPoints = BuildActualPoints(Meter, pace, padL, padT, w, h);
        if (actualPoints.Count >= 2)
        {
            var poly = new Polyline
            {
                Stroke = VerdictBrush(pace.Verdict),
                StrokeThickness = 2.5,
                Points = new PointCollection(actualPoints)
            };
            Children.Add(poly);

            // Fill under actual vs recommended hint
            var area = new Polygon
            {
                Points = new PointCollection(actualPoints.Concat(new[]
                {
                    new Point(actualPoints[^1].X, padT + h),
                    new Point(actualPoints[0].X, padT + h)
                })),
                Fill = new SolidColorBrush(VerdictBrush(pace.Verdict).Color) { Opacity = 0.12 }
            };
            Children.Insert(0, area);
        }

        // Now marker
        if (Meter.Start is not null && Meter.End is not null && Meter.End > Meter.Start)
        {
            var frac = Math.Clamp(
                (DateTimeOffset.UtcNow - Meter.Start.Value).TotalSeconds
                / (Meter.End.Value - Meter.Start.Value).TotalSeconds,
                0, 1);
            var x = padL + w * frac;
            var y = padT + h * (1 - Math.Clamp(pace.ActualPercent, 0, 100) / 100.0);
            Children.Add(new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = VerdictBrush(pace.Verdict),
                Margin = new Thickness(x - 4, y - 4, 0, 0)
            });
        }

        Children.Add(new TextBlock
        {
            Text = "recommended →",
            FontSize = 9,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA2, 0x9E)),
            Margin = new Thickness(padL + 4, padT + 2, 0, 0)
        });
    }

    private static List<Point> BuildActualPoints(
        UsageMeter meter,
        PaceSnapshot pace,
        double padL,
        double padT,
        double w,
        double h)
    {
        var points = new List<Point>();
        if (meter.Start is null || meter.End is null || meter.End <= meter.Start)
            return points;

        var start = meter.Start.Value;
        var end = meter.End.Value;
        var span = (end - start).TotalSeconds;
        if (span <= 0) return points;

        if (meter.Series.Count >= 2)
        {
            foreach (var p in meter.Series)
            {
                var fx = Math.Clamp((p.Timestamp - start).TotalSeconds / span, 0, 1);
                var fy = meter.Limit > 0
                    ? Math.Clamp(p.Used / meter.Limit * 100, 0, 120) / 100.0
                    : 0;
                points.Add(new Point(padL + w * fx, padT + h * (1 - Math.Min(fy, 1))));
            }
            return points;
        }

        // No history yet: 0 at start → current at now
        var nowFrac = Math.Clamp((DateTimeOffset.UtcNow - start).TotalSeconds / span, 0, 1);
        points.Add(new Point(padL, padT + h));
        points.Add(new Point(
            padL + w * nowFrac,
            padT + h * (1 - Math.Clamp(pace.ActualPercent, 0, 100) / 100.0)));
        return points;
    }

    private static SolidColorBrush VerdictBrush(PaceVerdict verdict) =>
        verdict switch
        {
            PaceVerdict.Over => new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C)),
            PaceVerdict.Under => new SolidColorBrush(Color.FromRgb(0x0F, 0x76, 0x6E)),
            _ => new SolidColorBrush(Color.FromRgb(0xA1, 0x62, 0x07))
        };
}
