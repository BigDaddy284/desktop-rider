using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DesktopRider.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the services every part of Desktop Rider relies on: file locations, logging and settings.</summary>
    public static IServiceCollection AddDesktopRiderCore(this IServiceCollection services, AppPaths paths, LogLevel minimumLogLevel = LogLevel.Information)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(minimumLogLevel);
            logging.Services.AddSingleton<ILoggerProvider>(provider =>
                new FileLoggerProvider(paths.LogDirectory, minimumLogLevel, timeProvider: provider.GetRequiredService<TimeProvider>()));
        });
        services.AddSingleton<ISettingsStore>(provider => new JsonSettingsStore(
            paths,
            provider.GetRequiredService<ILogger<JsonSettingsStore>>(),
            provider.GetRequiredService<TimeProvider>()));

        return services;
    }
}
