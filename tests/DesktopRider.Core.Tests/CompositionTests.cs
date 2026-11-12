using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DesktopRider.Core.Tests;

public class CompositionTests
{
    [Fact]
    public async Task CoreServices_ResolveAndWorkTogether()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var provider = new ServiceCollection().AddDesktopRiderCore(paths).BuildServiceProvider(validateScopes: true);
        await using (provider.ConfigureAwait(true))
        {
            var store = provider.GetRequiredService<ISettingsStore>();
            Assert.Same(store, provider.GetRequiredService<ISettingsStore>());
            Assert.Same(paths, provider.GetRequiredService<AppPaths>());

            var result = await store.LoadAsync();
            Assert.Equal(SettingsLoadStatus.Defaults, result.Status);

            provider.GetRequiredService<ILogger<CompositionTests>>().LogInformation("composition works");
        }

        // The store reported that it fell back to defaults, and the test's own entry was written, both to the local log file.
        var log = string.Concat(Directory.GetFiles(paths.LogDirectory).Select(File.ReadAllText));
        Assert.Contains("No settings file found", log, StringComparison.Ordinal);
        Assert.Contains("composition works", log, StringComparison.Ordinal);
    }

    [Fact]
    public void CoreServices_RespectTheMinimumLogLevel()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        using var provider = new ServiceCollection().AddDesktopRiderCore(paths, LogLevel.Error).BuildServiceProvider();

        provider.GetRequiredService<ILogger<CompositionTests>>().LogWarning("not important enough");

        Assert.False(Directory.Exists(paths.LogDirectory));
    }

    [Fact]
    public void SingleInstanceGuard_OnlyTheFirstHolderIsTheFirstInstance()
    {
        var name = "DesktopRider.Tests." + Guid.NewGuid().ToString("N");

        using var first = new SingleInstanceGuard(name);
        using var second = new SingleInstanceGuard(name);

        Assert.True(first.IsFirstInstance);
        Assert.False(second.IsFirstInstance);
    }

    [Fact]
    public void SingleInstanceGuard_CanBeTakenAgainAfterRelease()
    {
        var name = "DesktopRider.Tests." + Guid.NewGuid().ToString("N");

        using (var first = new SingleInstanceGuard(name))
        {
            Assert.True(first.IsFirstInstance);
            first.Dispose(); // Disposing twice is harmless.
        }

        using var next = new SingleInstanceGuard(name);
        Assert.True(next.IsFirstInstance);
    }
}
