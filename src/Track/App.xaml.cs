using System.Windows;
using Track.Core.Adapters;
using Track.Core.Services;
using Track.Core.Storage;

namespace Track;

public partial class App : Application
{
    public static UsageService Usage { get; private set; } = null!;
    public static SnapshotStore Store { get; private set; } = null!;
    public static ICredentialStore Credentials { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Credentials = new InMemoryCredentialStore();
        Store = new SnapshotStore();

        var adapters = new IProviderAdapter[]
        {
            new CursorSessionAdapter(),
            new OpenAIAdminAdapter(),
            new AnthropicAdminAdapter()
        };

        Usage = new UsageService(adapters, TimeSpan.FromMinutes(5));
        Usage.Start();

        // Settings window is created but starts hidden; tray owns the lifetime.
        var settings = new SettingsWindow();
        MainWindow = settings;

        var flyout = new FlyoutWindow();
        _ = new TrayController(settings, flyout);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Usage.Dispose();
        Store.Dispose();
        base.OnExit(e);
    }
}
