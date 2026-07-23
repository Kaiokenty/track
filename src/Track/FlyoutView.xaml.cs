using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Track.Controls;
using Track.Core.Models;
using Track.Core.Pace;

namespace Track;

public partial class FlyoutView : UserControl
{
    private bool _weekly;
    private bool _ready;

    public FlyoutView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_ready) return;
            _ready = true;
            WindowMonthly.IsChecked = true;
            WindowMonthly.Checked += OnWindowChanged;
            WindowWeekly.Checked += OnWindowChanged;
            Reload();
        };
    }

    public void Reload()
    {
        if (ProvidersPanel is null) return;
        ProvidersPanel.Children.Clear();

        var snaps = App.Usage.GetCachedSnapshots()
            .Where(s => s.Status is not ProviderStatus.NotLinked || s.Id == "cursor")
            .ToList();

        var latest = snaps.Where(s => s.Status is ProviderStatus.Ok or ProviderStatus.Stale)
            .MaxBy(s => s.FetchedAt)?.FetchedAt;
        RefreshedLabel.Text = latest is null
            ? "Not refreshed"
            : $"Updated {latest.Value.ToLocalTime():t}";

        var linked = snaps
            .Where(s => s.Status is ProviderStatus.Ok or ProviderStatus.Stale or ProviderStatus.Error)
            .ToList();

        if (linked.Count == 0)
        {
            ProvidersPanel.Children.Add(Muted(
                "No usage yet. Open Cursor while signed in, then right-click tray → Refresh."));
            return;
        }

        foreach (var snap in linked)
            ProvidersPanel.Children.Add(BuildProviderCard(snap));
    }

    private void OnWindowChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _weekly = WindowWeekly?.IsChecked == true;
        Reload();
    }

    private UIElement BuildProviderCard(ProviderSnapshot snap)
    {
        var border = new Border
        {
            Background = (Brush)Application.Current.Resources["BgBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 8)
        };

        var stack = new StackPanel();

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var plan = new TextBlock
        {
            Text = snap.PlanLabel ?? snap.Status.ToString(),
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["MutedBrush"],
            VerticalAlignment = VerticalAlignment.Center
        };
        DockPanel.SetDock(plan, Dock.Right);
        header.Children.Add(plan);
        header.Children.Add(new TextBlock
        {
            Text = snap.DisplayName,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["FgBrush"]
        });
        stack.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(snap.StatusMessage) && snap.Status is not ProviderStatus.Ok)
        {
            stack.Children.Add(new TextBlock
            {
                Text = snap.StatusMessage,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(0, 0, 0, 6)
            });
        }

        var focus = PickFocusMeter(snap);
        if (focus is not null)
        {
            var pace = PaceCalculator.Calculate(focus);
            var enriched = EnrichWithHistory(snap.Id, focus);

            stack.Children.Add(new TextBlock
            {
                Text = $"{focus.Label}: {FormatRemaining(focus)} · {FormatVerdict(pace)}",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["FgBrush"]
            });

            stack.Children.Add(new TextBlock
            {
                Text =
                    $"Recommended {pace.RecommendedPercent:0}% · actual {pace.ActualPercent:0}% ({pace.DeltaPercent:+0;-0;0}%)"
                    + (focus.ResetsAt is null ? "" : $" · resets {FormatCountdown(focus.ResetsAt.Value)}"),
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(0, 2, 0, 6),
                TextWrapping = TextWrapping.Wrap
            });

            if (pace.ProjectedExhaustion is not null && pace.Verdict is not PaceVerdict.Under)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = $"At this rate, limit around {pace.ProjectedExhaustion.Value.ToLocalTime():ddd g}",
                    FontSize = 11,
                    Foreground = (Brush)Application.Current.Resources["MutedBrush"],
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }

            stack.Children.Add(new PaceChartControl
            {
                Meter = enriched,
                Width = 320,
                Height = 140,
                HorizontalAlignment = HorizontalAlignment.Left
            });
        }

        foreach (var meter in snap.Meters.Where(m => focus is null || m.Id != focus.Id))
        {
            if (_weekly && meter.Kind is MeterKind.Monthly && meter.Id is "auto" or "api")
                continue;
            if (!_weekly && meter.Kind is MeterKind.Weekly)
                continue;

            var pace = PaceCalculator.Calculate(meter);
            stack.Children.Add(new TextBlock
            {
                Text = $"{meter.Label}: {FormatRemaining(meter)} · {pace.Verdict} pace",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["FgBrush"],
                Margin = new Thickness(0, 6, 0, 0)
            });
        }

        foreach (var extra in snap.Extras)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"{extra.Label}: {extra.Value}",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["MutedBrush"],
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        border.Child = stack;
        return border;
    }

    private UsageMeter? PickFocusMeter(ProviderSnapshot snap)
    {
        if (_weekly)
            return snap.Meters.FirstOrDefault(m => m.Kind == MeterKind.Weekly)
                   ?? snap.Meters.FirstOrDefault(m => m.Id == "total");

        return snap.Meters.FirstOrDefault(m => m.Id == "total")
               ?? snap.Meters.FirstOrDefault(m => m.Kind == MeterKind.Monthly);
    }

    private static UsageMeter EnrichWithHistory(string providerId, UsageMeter meter)
    {
        if (meter.Start is null) return meter;
        try
        {
            var series = App.Store.GetSeries(providerId, meter.Id, meter.Start.Value);
            if (series.Count == 0) return meter;
            return meter with { Series = series };
        }
        catch
        {
            return meter;
        }
    }

    private static string FormatRemaining(UsageMeter meter) =>
        meter.Unit switch
        {
            MeterUnit.UsdCents =>
                $"${meter.Remaining / 100.0:0.00} left (${meter.Used / 100.0:0.00}/${meter.Limit / 100.0:0.00})",
            MeterUnit.Requests =>
                $"{meter.Remaining:0} left ({meter.Used:0}/{meter.Limit:0})",
            _ => $"{meter.PercentRemaining:0}% left"
        };

    private static string FormatVerdict(PaceSnapshot pace) =>
        pace.Verdict switch
        {
            PaceVerdict.Over => "over pace",
            PaceVerdict.Under => "under pace",
            _ => "on track"
        };

    private static TextBlock Muted(string text) => new()
    {
        Text = text,
        Foreground = (Brush)Application.Current.Resources["MutedBrush"],
        TextWrapping = TextWrapping.Wrap
    };

    private static string FormatCountdown(DateTimeOffset resetsAt)
    {
        var remaining = resetsAt - DateTimeOffset.UtcNow;
        if (remaining.TotalSeconds <= 0) return "now";
        if (remaining.TotalDays >= 1) return $"in {(int)remaining.TotalDays}d {remaining.Hours}h";
        if (remaining.TotalHours >= 1) return $"in {(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"in {(int)remaining.TotalMinutes}m";
    }
}
