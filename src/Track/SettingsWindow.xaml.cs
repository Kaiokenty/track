using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Track.Core.Models;

namespace Track;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ReloadConnections();
        App.Usage.SnapshotsChanged += (_, _) =>
            Dispatcher.Invoke(ReloadConnections);
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Closing settings should not quit the tray app.
        e.Cancel = true;
        Hide();
    }

    private void OnNavConnections(object sender, RoutedEventArgs e)
    {
        SectionTitle.Text = "Connections";
        ConnectionsPanel.Visibility = Visibility.Visible;
        GeneralPanel.Visibility = Visibility.Collapsed;
        NavConnections.Foreground = Brushes.White;
        NavGeneral.Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA2, 0x9E));
    }

    private void OnNavGeneral(object sender, RoutedEventArgs e)
    {
        SectionTitle.Text = "General";
        ConnectionsPanel.Visibility = Visibility.Collapsed;
        GeneralPanel.Visibility = Visibility.Visible;
        NavGeneral.Foreground = Brushes.White;
        NavConnections.Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA2, 0x9E));
    }

    private void ReloadConnections()
    {
        ConnectionsPanel.Children.Clear();
        foreach (var snap in App.Usage.GetCachedSnapshots())
            ConnectionsPanel.Children.Add(BuildConnectionRow(snap));
    }

    private UIElement BuildConnectionRow(ProviderSnapshot snap)
    {
        var border = new Border
        {
            Background = (Brush)FindResource("CardBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = snap.DisplayName,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("FgBrush")
        });
        text.Children.Add(new TextBlock
        {
            Text = StatusLine(snap),
            FontSize = 12,
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var button = new Button
        {
            Content = snap.Status is ProviderStatus.Ok or ProviderStatus.Stale ? "Disconnect" : "Connect",
            Padding = new Thickness(12, 6, 12, 6),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = snap.Id == "cursor"
        };
        button.Click += async (_, _) =>
        {
            await App.Usage.RefreshNowAsync();
            MessageBox.Show(
                snap.Id == "cursor"
                    ? "Cursor connect will read the local session token in Phase 1.\nRefresh ran — see status in the tray flyout."
                    : "API key linking lands in Phase 2.",
                "Track",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        };
        Grid.SetColumn(button, 1);
        grid.Children.Add(button);

        border.Child = grid;
        return border;
    }

    private static string StatusLine(ProviderSnapshot snap)
    {
        var mode = snap.Mode switch
        {
            ProviderMode.Subscription => "Subscription",
            ProviderMode.ApiCost => "API cost",
            ProviderMode.Manual => "Manual",
            _ => snap.Mode.ToString()
        };
        return $"{mode} · {snap.Status}" +
               (string.IsNullOrWhiteSpace(snap.StatusMessage) ? "" : $" — {snap.StatusMessage}");
    }
}
