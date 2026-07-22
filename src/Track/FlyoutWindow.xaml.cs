using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Track.Core.Models;
using Track.Core.Pace;

namespace Track;

public partial class FlyoutWindow : Window
{
    public FlyoutWindow()
    {
        InitializeComponent();
    }

    public void Reload()
    {
        ProvidersPanel.Children.Clear();
        var snaps = App.Usage.GetCachedSnapshots();
        var latest = snaps.MaxBy(s => s.FetchedAt)?.FetchedAt;
        RefreshedLabel.Text = latest is null
            ? "Not refreshed"
            : $"Updated {latest.Value.ToLocalTime():t}";

        if (snaps.Count == 0)
        {
            ProvidersPanel.Children.Add(Muted("No providers configured."));
            return;
        }

        foreach (var snap in snaps)
            ProvidersPanel.Children.Add(BuildProviderCard(snap));
    }

    private void OnDeactivated(object sender, EventArgs e) => Hide();

    private static UIElement BuildProviderCard(ProviderSnapshot snap)
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

        if (!string.IsNullOrWhiteSpace(snap.StatusMessage))
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

        foreach (var meter in snap.Meters)
        {
            var pace = PaceCalculator.Calculate(meter);
            var reset = meter.ResetsAt is null
                ? ""
                : $" · resets {FormatCountdown(meter.ResetsAt.Value)}";

            stack.Children.Add(new TextBlock
            {
                Text = $"{meter.Label}: {meter.PercentRemaining:0}% left · {pace.Verdict} pace{reset}",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["FgBrush"],
                Margin = new Thickness(0, 2, 0, 0)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"Recommended {pace.RecommendedPercent:0}% · actual {pace.ActualPercent:0}% ({pace.DeltaPercent:+0;-0}%)",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["MutedBrush"]
            });
        }

        border.Child = stack;
        return border;
    }

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
