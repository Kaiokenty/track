using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace Track;

public sealed class TrayController : IDisposable
{
    private readonly SettingsWindow _settings;
    private readonly FlyoutView _flyout;
    private readonly TaskbarIcon _tray;

    public TrayController(SettingsWindow settings)
    {
        _settings = settings;
        _flyout = new FlyoutView();

        _tray = new TaskbarIcon
        {
            ToolTipText = "Track — AI usage",
            IconSource = LoadIcon(),
            ContextMenu = BuildMenu(),
            TrayPopup = _flyout,
            MenuActivation = PopupActivationMode.RightClick,
            PopupActivation = PopupActivationMode.LeftClick,
            NoLeftClickDelay = true,
            Visibility = Visibility.Visible
        };

        // Code-created TaskbarIcon is not in a visual tree, so Visibility alone does not
        // register the Win32 notify icon. ForceCreate materializes it (H.NotifyIcon).
        _tray.ForceCreate(enablesEfficiencyMode: false);

        _tray.TrayPopupOpen += (_, _) => _flyout.Reload();
        _tray.TrayMouseDoubleClick += (_, _) => ShowSettings();

        App.Usage.SnapshotsChanged += (_, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _tray.ToolTipText = BuildTooltip();
                if (_flyout.IsVisible)
                    _flyout.Reload();
            });
        };
    }

    public void Dispose() => _tray.Dispose();

    private void ShowSettings()
    {
        _settings.Show();
        _settings.WindowState = WindowState.Normal;
        _settings.Activate();
    }

    private void ShowUsageWindow()
    {
        // Fallback if tray popup is hard to discover: open as a normal window.
        var win = new Window
        {
            Title = "Track",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 700,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new FlyoutView()
        };
        ((FlyoutView)win.Content).Reload();
        win.Show();
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

        var openUsage = new MenuItem { Header = "Open usage" };
        openUsage.Click += (_, _) => ShowUsageWindow();

        var refresh = new MenuItem { Header = "Refresh" };
        refresh.Click += async (_, _) => await App.Usage.RefreshNowAsync();

        var openSettings = new MenuItem { Header = "Settings" };
        openSettings.Click += (_, _) => ShowSettings();

        var quit = new MenuItem { Header = "Quit" };
        quit.Click += (_, _) =>
        {
            _tray.Dispose();
            Application.Current.Shutdown();
        };

        menu.Items.Add(openUsage);
        menu.Items.Add(refresh);
        menu.Items.Add(openSettings);
        menu.Items.Add(new Separator());
        menu.Items.Add(quit);
        return menu;
    }

    private static string BuildTooltip()
    {
        var snaps = App.Usage.GetCachedSnapshots();
        var cursor = snaps.FirstOrDefault(s => s.Id == "cursor");
        if (cursor?.Meters.FirstOrDefault(m => m.Id == "total") is { } meter)
            return $"Track — Cursor {meter.PercentUsed:0}% · {cursor.Status}";
        return "Track — AI usage";
    }

    private static BitmapFrame LoadIcon()
    {
        var uri = new Uri("pack://application:,,,/Assets/track.ico", UriKind.Absolute);
        try
        {
            return BitmapFrame.Create(uri);
        }
        catch
        {
            // Unpackaged / missing resource fallback: load from disk next to the exe.
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "track.ico");
            if (!File.Exists(path))
                path = Path.Combine(AppContext.BaseDirectory, "track.ico");
            return BitmapFrame.Create(new Uri(path, UriKind.Absolute));
        }
    }
}
