#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using DesktopRider.Core.Logging;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public sealed class FileLoggerProviderTests
    {
        private TempDirectory _temp = null!;
        private FakeTimeProvider _time = null!;

        private string LogDirectory => Path.Combine(_temp.Path, "Logs");

        // NUnit uses one object for every test in the class, so each test is given a fresh folder and clock here.
        [SetUp]
        public void SetUp()
        {
            _temp = new TempDirectory();
            _time = new FakeTimeProvider(new DateTimeOffset(2026, 3, 10, 14, 30, 15, 250, TimeSpan.Zero));
        }

        [TearDown]
        public void TearDown() => _temp.Dispose();

        [Test]
        public void Log_WritesATimestampedLineToTodaysFile()
        {
            using var provider = new FileLoggerProvider(LogDirectory, timeProvider: _time);

            provider.CreateLogger("Startup").LogInformation("Hello {Name}", "rider");

            var text = File.ReadAllText(Path.Combine(LogDirectory, "desktop-rider-20260310.log"));
            Assert.Equal("2026-03-10 14:30:15.250 [INFO] Startup: Hello rider" + Environment.NewLine, text);
        }

        [Test]
        public void Log_IncludesExceptionDetails()
        {
            using var provider = new FileLoggerProvider(LogDirectory, timeProvider: _time);

            provider.CreateLogger("Test").LogError(new InvalidOperationException("boom"), "Something failed");

            var text = File.ReadAllText(Path.Combine(LogDirectory, "desktop-rider-20260310.log"));
            Assert.Contains("[ERROR] Test: Something failed", text, StringComparison.Ordinal);
            Assert.Contains("System.InvalidOperationException: boom", text, StringComparison.Ordinal);
        }

        [Test]
        public void Log_SkipsEntriesBelowTheMinimumLevel()
        {
            using var provider = new FileLoggerProvider(LogDirectory, LogLevel.Warning, timeProvider: _time);
            var logger = provider.CreateLogger("Test");

            logger.LogInformation("quiet");
            logger.LogWarning("loud");

            Assert.False(logger.IsEnabled(LogLevel.Information));
            Assert.False(logger.IsEnabled(LogLevel.None));
            var text = File.ReadAllText(Path.Combine(LogDirectory, "desktop-rider-20260310.log"));
            Assert.DoesNotContain("quiet", text, StringComparison.Ordinal);
            Assert.Contains("[WARN] Test: loud", text, StringComparison.Ordinal);
        }

        [Test]
        public void Log_StartsANewFileEachDay()
        {
            using var provider = new FileLoggerProvider(LogDirectory, timeProvider: _time);
            var logger = provider.CreateLogger("Test");

            logger.LogInformation("day one");
            _time.Now = _time.Now.AddDays(1);
            logger.LogInformation("day two");

            Assert.Contains("day one", File.ReadAllText(Path.Combine(LogDirectory, "desktop-rider-20260310.log")), StringComparison.Ordinal);
            Assert.Contains("day two", File.ReadAllText(Path.Combine(LogDirectory, "desktop-rider-20260311.log")), StringComparison.Ordinal);
        }

        [Test]
        public void Log_RemovesFilesOlderThanTheRetentionPeriod_AndLeavesOtherFilesAlone()
        {
            Directory.CreateDirectory(LogDirectory);
            var old = Path.Combine(LogDirectory, "desktop-rider-20260301.log");
            var boundary = Path.Combine(LogDirectory, "desktop-rider-20260308.log");
            var unrelated = Path.Combine(LogDirectory, "notes.txt");
            var oddName = Path.Combine(LogDirectory, "desktop-rider-custom.log");
            foreach (var file in new[] { old, boundary, unrelated, oddName })
            {
                File.WriteAllText(file, "x");
            }

            using var provider = new FileLoggerProvider(LogDirectory, retainedDays: 3, timeProvider: _time);
            provider.CreateLogger("Test").LogInformation("trigger cleanup");

            Assert.False(File.Exists(old));
            Assert.True(File.Exists(boundary)); // 8th, 9th and 10th are the three retained days.
            Assert.True(File.Exists(unrelated));
            Assert.True(File.Exists(oddName));
        }

        [Test]
        public void Log_FromManyThreads_KeepsEveryLineIntact()
        {
            using var provider = new FileLoggerProvider(LogDirectory, timeProvider: _time);
            var logger = provider.CreateLogger("Test");

            Parallel.For(0, 200, i => logger.LogInformation("entry {Number}", i));

            var lines = File.ReadAllLines(Path.Combine(LogDirectory, "desktop-rider-20260310.log"));
            Assert.Equal(200, lines.Length);
            Assert.All(lines, line => Assert.Matches(@"\[INFO\] Test: entry \d+$", line));
        }

        [Test]
        public void Log_WhenTheFolderCannotBeUsed_DoesNotThrow()
        {
            // A file sits where the log folder should be, so every write fails.
            var blocked = Path.Combine(_temp.Path, "blocked");
            File.WriteAllText(blocked, "x");
            using var provider = new FileLoggerProvider(blocked, timeProvider: _time);

            var exception = Record.Exception(() => provider.CreateLogger("Test").LogInformation("ignored"));

            Assert.Null(exception);
        }

        [Test]
        public void Log_AfterDispose_WritesNothing()
        {
            var provider = new FileLoggerProvider(LogDirectory, timeProvider: _time);
            var logger = provider.CreateLogger("Test");
            provider.Dispose();

            logger.LogInformation("too late");

            Assert.False(Directory.Exists(LogDirectory));
        }

        [Test]
        public void CreateLogger_ReturnsTheSameLoggerForTheSameCategory()
        {
            using var provider = new FileLoggerProvider(LogDirectory, timeProvider: _time);

            Assert.Same(provider.CreateLogger("A"), provider.CreateLogger("A"));
            Assert.NotSame(provider.CreateLogger("A"), provider.CreateLogger("B"));
        }
    }
}
