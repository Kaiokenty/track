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

    private TrayController? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Track error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            Credentials = new InMemoryCredentialStore();
            Store = new SnapshotStore();

            var adapters = new IProviderAdapter[]
            {
                new CursorSessionAdapter(),
                new OpenAIAdminAdapter(),
                new AnthropicAdminAdapter()
            };

            Usage = new UsageService(adapters, TimeSpan.FromMinutes(5), Store);
            Usage.Start();

            var settings = new SettingsWindow();
            MainWindow = settings;
            _tray = new TrayController(settings);
            settings.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "Track failed to start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        Usage?.Dispose();
        Store?.Dispose();
        base.OnExit(e);
    }
}
