using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace Track;

public sealed class TrayController : IDisposable
{
    private readonly SettingsWindow _settings;
    private readonly FlyoutWindow _flyout;
    private readonly TaskbarIcon _tray;

    public TrayController(SettingsWindow settings, FlyoutWindow flyout)
    {
        _settings = settings;
        _flyout = flyout;

        _tray = new TaskbarIcon
        {
            ToolTipText = "Track — AI usage",
            Icon = GenerateIcon(),
            ContextMenu = BuildMenu(),
            MenuActivation = PopupActivationMode.RightClick,
            NoLeftClickDelay = true
        };

        _tray.TrayMouseDoubleClick += (_, _) => ShowSettings();
        _tray.TrayLeftMouseUp += (_, _) => ToggleFlyout();

        App.Usage.SnapshotsChanged += (_, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _tray.ToolTipText = BuildTooltip();
                _flyout.Reload();
            });
        };
    }

    public void Dispose() => _tray.Dispose();

    private void ToggleFlyout()
    {
        if (_flyout.IsVisible)
        {
            _flyout.Hide();
            return;
        }

        _flyout.Reload();
        PositionNearTray(_flyout);
        _flyout.Show();
        _flyout.Activate();
    }

    private void ShowSettings()
    {
        _settings.Show();
        _settings.WindowState = WindowState.Normal;
        _settings.Activate();
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();

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
        if (cursor?.Meters.FirstOrDefault() is { } meter)
            return $"Track — {cursor.DisplayName} {meter.PercentUsed:0}% · {cursor.Status}";
        return "Track — AI usage";
    }

    private static void PositionNearTray(Window window)
    {
        var work = SystemParameters.WorkArea;
        window.Left = work.Right - window.Width - 12;
        window.Top = work.Bottom - window.Height - 12;
    }

    private static System.Drawing.Icon GenerateIcon()
    {
        // Simple teal circle — replace with branded .ico later.
        var bmp = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            g.Clear(System.Drawing.Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(15, 118, 110));
            g.FillEllipse(brush, 2, 2, 28, 28);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2);
            g.DrawEllipse(pen, 8, 8, 16, 16);
        }

        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }
}
