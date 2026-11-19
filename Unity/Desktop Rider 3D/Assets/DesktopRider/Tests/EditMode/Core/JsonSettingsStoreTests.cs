#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public sealed class JsonSettingsStoreTests
    {
        private TempDirectory _temp = null!;
        private AppPaths _paths = null!;
        private JsonSettingsStore _store = null!;

        // NUnit uses one object for every test in the class, so each test is given a fresh folder and store here.
        [SetUp]
        public void SetUp()
        {
            _temp = new TempDirectory();
            _paths = new AppPaths(Path.Combine(_temp.Path, "data"));
            _store = new JsonSettingsStore(_paths, timeProvider: new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
        }

        [TearDown]
        public void TearDown()
        {
            _store.Dispose();
            _temp.Dispose();
        }

        [Test]
        public async Task Load_WithNoFile_ReturnsDefaultsWithoutCreatingAnything()
        {
            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Defaults, result.Status);
            Assert.True(result.Settings.Appearance.AlwaysOnTop);
            Assert.False(File.Exists(_paths.SettingsFile));
        }

        [Test]
        public async Task SaveThenLoad_RoundTripsEveryValue()
        {
            var settings = new AppSettings();
            settings.Appearance.Scale = 1.1;
            settings.Appearance.Opacity = 0.7;
            settings.Appearance.AlwaysOnTop = false;
            settings.Appearance.ClickThrough = true;
            settings.Appearance.Position = new SavedPosition { X = -320.5, Y = 48, Monitor = @"\\.\DISPLAY2" };
            settings.Behaviour.AnimationFrequency = AnimationFrequency.Frequent;
            settings.Behaviour.FreeRiding = true;
            settings.Behaviour.TricksByHimself = TrickFrequency.Often;
            settings.Application.StartWithWindows = true;
            settings.Application.TrayHintShown = true;

            await _store.SaveAsync(settings);
            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            var loaded = result.Settings;
            Assert.Equal(1.1, loaded.Appearance.Scale);
            Assert.Equal(0.7, loaded.Appearance.Opacity);
            Assert.False(loaded.Appearance.AlwaysOnTop);
            Assert.True(loaded.Appearance.ClickThrough);
            Assert.Equal(-320.5, loaded.Appearance.Position!.X);
            Assert.Equal(48, loaded.Appearance.Position.Y);
            Assert.Equal(@"\\.\DISPLAY2", loaded.Appearance.Position.Monitor);
            Assert.Equal(AnimationFrequency.Frequent, loaded.Behaviour.AnimationFrequency);
            Assert.True(loaded.Behaviour.FreeRiding);
            Assert.Equal(TrickFrequency.Often, loaded.Behaviour.TricksByHimself);
            Assert.True(loaded.Application.StartWithWindows);
            Assert.True(loaded.Application.TrayHintShown);
        }

        [Test]
        public async Task Save_CreatesTheDataFolder_AndLeavesNoTemporaryFile()
        {
            await _store.SaveAsync(new AppSettings());

            Assert.True(File.Exists(_paths.SettingsFile));
            Assert.Equal(new[] { "settings.json" }, Directory.GetFiles(_paths.DataDirectory).Select(Path.GetFileName));
        }

        [Test]
        public async Task Save_WritesReadableJson()
        {
            var settings = new AppSettings();
            settings.Behaviour.AnimationFrequency = AnimationFrequency.Rare;

            await _store.SaveAsync(settings);
            var json = await File.ReadAllTextAsync(_paths.SettingsFile);

            Assert.Contains("\"alwaysOnTop\": true", json, StringComparison.Ordinal);
            Assert.DoesNotContain("ollama", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"animationFrequency\": \"rare\"", json, StringComparison.Ordinal);
            Assert.Contains(Environment.NewLine, json, StringComparison.Ordinal);
        }

        [Test]
        public async Task Save_NormalizesValuesBeforeWriting()
        {
            var settings = new AppSettings();
            settings.Appearance.Scale = 500;

            await _store.SaveAsync(settings);

            Assert.Equal(AppearanceSettings.MaxScale, (await _store.LoadAsync()).Settings.Appearance.Scale);
        }
        [TestCase("{ this is not json")]
        [TestCase("")]
        [TestCase("null")]
        [TestCase("[1, 2, 3]")]
        public async Task Load_WithDamagedFile_UsesDefaultsAndKeepsABackup(string contents)
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            await File.WriteAllTextAsync(_paths.SettingsFile, contents);

            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.RecoveredFromDamagedFile, result.Status);
            Assert.Equal(AppearanceSettings.DefaultScale, result.Settings.Appearance.Scale);
            Assert.False(File.Exists(_paths.SettingsFile));
            Assert.Equal(Path.Combine(_paths.DataDirectory, "settings.damaged-20260102-030405.json"), result.BackupFile);
            Assert.Equal(contents, await File.ReadAllTextAsync(result.BackupFile!));
        }

        [Test]
        public async Task Load_FillsInValuesMissingFromAnOlderFile()
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            await File.WriteAllTextAsync(_paths.SettingsFile, "{ \"appearance\": { \"scale\": 1.2 } }");

            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.Equal(1.2, result.Settings.Appearance.Scale);
            Assert.True(result.Settings.Appearance.AlwaysOnTop);
        }

        [Test]
        public async Task Load_ToleratesCommentsUnknownValuesAndBadRanges()
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            await File.WriteAllTextAsync(
                _paths.SettingsFile,
                @"{
  // edited by hand
  ""somethingFromTheFuture"": true,
  ""appearance"": { ""scale"": 99, },
}");

            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.Equal(AppearanceSettings.MaxScale, result.Settings.Appearance.Scale);
        }

        [Test]
        public async Task Load_AFileFromAVersionWithChat_StillLoads_AndKeepsTheRidersSettings()
        {
            // Version 2 has no chat or AI; a file written by a version that had them (version 1, or version 3 later) must
            // still load, its chat parts simply ignored.
            Directory.CreateDirectory(_paths.DataDirectory);
            await File.WriteAllTextAsync(_paths.SettingsFile,
                "{ \"appearance\": { \"scale\": 1.3, \"chatInBubbles\": true },"
                + " \"behaviour\": { \"freeRiding\": true, \"proactiveComments\": true, \"proactiveIntervalMinutes\": 45 },"
                + " \"ai\": { \"ollamaAddress\": \"http://localhost:11434\", \"model\": \"some-model\" },"
                + " \"memory\": { \"retentionDays\": 7 } }");

            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.Equal(1.3, result.Settings.Appearance.Scale);
            Assert.True(result.Settings.Behaviour.FreeRiding);
        }

        [Test]
        public async Task Save_OverwritesThePreviousFile()
        {
            var settings = new AppSettings();
            await _store.SaveAsync(settings);
            settings.Appearance.Scale = 1.15;
            await _store.SaveAsync(settings);

            Assert.Equal(1.15, (await _store.LoadAsync()).Settings.Appearance.Scale);
        }

        [Test]
        public async Task ConcurrentSaves_NeverCorruptTheFile()
        {
            var saves = Enumerable.Range(0, 25).Select(i =>
            {
                var settings = new AppSettings();
                settings.Appearance.Scale = 0.5 + i * 0.05;
                return _store.SaveAsync(settings);
            });

            await Task.WhenAll(saves);
            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.InRange(result.Settings.Appearance.Scale, 0.5, 0.5 + 24 * 0.05 + 1e-9);
        }

        [Test]
        public async Task Save_HonoursCancellation()
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.SaveAsync(new AppSettings(), cancellation.Token));
            Assert.False(File.Exists(_paths.SettingsFile));
        }

        [Test]
        public async Task Save_RejectsNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SaveAsync(null!));
        }
    }
}
