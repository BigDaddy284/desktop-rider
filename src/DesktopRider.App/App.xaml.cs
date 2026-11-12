using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DesktopRider.Core;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.Placement;
using DesktopRider.Core.ViewModels;
using DesktopRider.Desktop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DesktopRider.App;

/// <summary>Application entry point: starts the shared services, shows the character's window and shuts everything down cleanly.</summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF owns the application lifetime; the fields are disposed in OnExit.")]
public partial class App : Application
{
    private SingleInstanceGuard? _instanceGuard;
    private ServiceProvider? _services;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceGuard = new SingleInstanceGuard();
        if (!_instanceGuard.IsFirstInstance)
        {
            MessageBox.Show(
                $"{AppInfo.Name} is already running. Look for the rider on your desktop.",
                AppInfo.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            var paths = AppPaths.ForCurrentUser();
            paths.EnsureDirectoriesExist();

            var services = new ServiceCollection();
            services.AddDesktopRiderCore(paths);
            services.AddSingleton<IScreenProvider, WindowsScreenProvider>();
            services.AddSingleton<OverlayViewModel>();
            services.AddSingleton<OverlayWindow>();
            _services = services.BuildServiceProvider();

            _logger = _services.GetRequiredService<ILogger<App>>();
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            Log.Starting(_logger, AppInfo.Version);

            var settings = _services.GetRequiredService<ISettingsService>();
            await settings.InitializeAsync();
            Log.SettingsReady(_logger, settings.LoadStatus);

            var window = _services.GetRequiredService<OverlayWindow>();
            MainWindow = window;
            window.Closed += (_, _) => Shutdown();
            window.Show();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (_logger is not null)
            {
                Log.StartupFailed(_logger, ex);
            }

            MessageBox.Show(
                $"{AppInfo.Name} could not start.\n\n{ex.Message}\n\nPlease try again. If this keeps happening, restart your computer.",
                AppInfo.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_logger is not null)
        {
            Log.Exiting(_logger, e.ApplicationExitCode);
        }

        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _services?.Dispose();
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Keep the rider running after an unexpected problem in the interface; the details go to the log file.
        if (_logger is not null)
        {
            Log.UnexpectedError(_logger, e.Exception);
        }

        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (_logger is not null && e.ExceptionObject is Exception exception)
        {
            Log.FatalError(_logger, exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (_logger is not null)
        {
            Log.UnexpectedError(_logger, e.Exception);
        }

        e.SetObserved();
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Desktop Rider {Version} is starting.")]
        public static partial void Starting(ILogger logger, string version);

        [LoggerMessage(Level = LogLevel.Information, Message = "Settings are ready ({Status}).")]
        public static partial void SettingsReady(ILogger logger, SettingsLoadStatus status);

        [LoggerMessage(Level = LogLevel.Information, Message = "Desktop Rider is closing with exit code {ExitCode}.")]
        public static partial void Exiting(ILogger logger, int exitCode);

        [LoggerMessage(Level = LogLevel.Critical, Message = "Desktop Rider could not start.")]
        public static partial void StartupFailed(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Error, Message = "An unexpected problem occurred. Desktop Rider is carrying on.")]
        public static partial void UnexpectedError(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Critical, Message = "A fatal problem occurred.")]
        public static partial void FatalError(ILogger logger, Exception exception);
    }
}
