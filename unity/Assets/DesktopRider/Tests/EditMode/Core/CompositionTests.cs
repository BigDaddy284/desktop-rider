#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.Logging;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class CompositionTests
    {
        [Test]
        public async Task CoreServices_ResolveAndWorkTogether()
        {
            using var temp = new TempDirectory();
            var paths = new AppPaths(temp.Path);
            using (var services = new CoreServices(paths))
            {
                var store = services.SettingsStore;
                Assert.Same(store, services.SettingsStore);
                Assert.Same(paths, services.Paths);

                var result = await store.LoadAsync();
                Assert.Equal(SettingsLoadStatus.Defaults, result.Status);

                services.Loggers.CreateLogger<CompositionTests>().LogInformation("composition works");
            }

            // The store reported that it fell back to defaults, and the test's own entry was written, both to the local log file.
            var log = string.Concat(Directory.GetFiles(paths.LogDirectory).Select(File.ReadAllText));
            Assert.Contains("No settings file found", log, StringComparison.Ordinal);
            Assert.Contains("composition works", log, StringComparison.Ordinal);
        }

        [Test]
        public void CoreServices_RespectTheMinimumLogLevel()
        {
            using var temp = new TempDirectory();
            var paths = new AppPaths(temp.Path);
            using var services = new CoreServices(paths, LogLevel.Error);

            services.Loggers.CreateLogger<CompositionTests>().LogWarning("not important enough");

            Assert.False(Directory.Exists(paths.LogDirectory));
        }

        [Test]
        public void SingleInstanceGuard_OnlyTheFirstHolderIsTheFirstInstance()
        {
            var name = "DesktopRider.Tests." + Guid.NewGuid().ToString("N");

            using var first = new SingleInstanceGuard(name);
            using var second = new SingleInstanceGuard(name);

            Assert.True(first.IsFirstInstance);
            Assert.False(second.IsFirstInstance);
        }

        [Test]
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
}
