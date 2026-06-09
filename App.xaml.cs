using System.Windows;
using ElectroScroll.Interop;
using ElectroScroll.Services;

namespace ElectroScroll;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private SettingsStore? _store;
    private Models.AppSettings? _settings;
    private PerformanceModeService? _performanceMode;
    private DiagnosticsLogger? _logger;
    private ScrollController? _controller;
    private LowLevelMouseHook? _hook;
    private TrayService? _tray;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        NativeMethods.SetProcessDpiAwarenessContext(NativeMethods.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, @"Global\ElectroScroll.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show(
                "ElectroScroll is already running.",
                "ElectroScroll",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var noHook = e.Args.Any(arg => string.Equals(arg, "--no-hook", StringComparison.OrdinalIgnoreCase));

        _store = new SettingsStore();
        _settings = _store.Load();
        _logger = new DiagnosticsLogger(_settings);
        _performanceMode = new PerformanceModeService(_settings);
        if (!noHook)
        {
            _performanceMode.Apply();
        }
        _controller = new ScrollController(_settings, _logger);

        if (!noHook)
        {
            _hook = new LowLevelMouseHook(_controller);

            try
            {
                _hook.Start();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"ElectroScroll could not install the global mouse hook.\n\n{ex.Message}",
                    "ElectroScroll",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
                return;
            }
        }

        _mainWindow = new MainWindow(_settings, _controller, _store, _performanceMode.Status, _logger);
        _tray = new TrayService(_mainWindow, _settings, _controller);

        if (!_settings.StartMinimized)
        {
            _mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose = true;
        }

        if (_settings is not null)
        {
            _store?.Save(_settings);
        }

        _tray?.Dispose();
        _hook?.Dispose();
        _controller?.Dispose();
        _logger?.Dispose();
        _performanceMode?.Dispose();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }
}
