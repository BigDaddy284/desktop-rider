#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    /// <summary>The version 2 setting that lets the rider ride freely around the screen.</summary>
    public sealed class FreeRidingSettingTests
    {
        private TempDirectory _temp = null!;
        private AppPaths _paths = null!;
        private JsonSettingsStore _store = null!;

        [SetUp]
        public void SetUp()
        {
            _temp = new TempDirectory();
            _paths = new AppPaths(Path.Combine(_temp.Path, "data"));
            _store = new JsonSettingsStore(_paths);
        }

        [TearDown]
        public void TearDown()
        {
            _store.Dispose();
            _temp.Dispose();
        }

        [Test]
        public void TheLogic_KnowsItIsVersion2()
        {
            // Unity stamps no version on the code it compiles, so this must come from the code itself.
            Assert.StartsWith("2.", AppInfo.Version, StringComparison.Ordinal);
        }

        [Test]
        public void FreeRiding_IsOffUntilTheUserSwitchesItOn()
        {
            Assert.False(new AppSettings().Behaviour.FreeRiding);
            Assert.False(new BehaviourSettings().FreeRiding);
            Assert.False(SettingsNormalizer.Normalize(null).Behaviour.FreeRiding);
        }

        [Test]
        public async Task FreeRiding_WithNoSettingsFile_IsOff()
        {
            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Defaults, result.Status);
            Assert.False(result.Settings.Behaviour.FreeRiding);
        }

        [Test]
        public async Task FreeRiding_SwitchedOn_IsStillOnAfterSavingAndLoading()
        {
            var settings = new AppSettings();
            settings.Behaviour.FreeRiding = true;

            await _store.SaveAsync(settings);
            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.True(result.Settings.Behaviour.FreeRiding);
            Assert.Contains("\"freeRiding\": true", await File.ReadAllTextAsync(_paths.SettingsFile), StringComparison.Ordinal);
        }

        [Test]
        public async Task FreeRiding_SwitchedOffAgain_IsSavedAsOff()
        {
            var settings = new AppSettings();
            settings.Behaviour.FreeRiding = true;
            await _store.SaveAsync(settings);

            settings.Behaviour.FreeRiding = false;
            await _store.SaveAsync(settings);

            Assert.False((await _store.LoadAsync()).Settings.Behaviour.FreeRiding);
            Assert.Contains("\"freeRiding\": false", await File.ReadAllTextAsync(_paths.SettingsFile), StringComparison.Ordinal);
        }

        [Test]
        public async Task FreeRiding_MissingFromAnOlderSettingsFile_IsOff_AndTheOtherChoicesAreKept()
        {
            Directory.CreateDirectory(_paths.DataDirectory);
            await File.WriteAllTextAsync(_paths.SettingsFile, "{ \"behaviour\": { \"idleAnimations\": false, \"proactiveIntervalMinutes\": 60 } }");

            var result = await _store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            Assert.False(result.Settings.Behaviour.FreeRiding);
            Assert.False(result.Settings.Behaviour.IdleAnimations);
            Assert.Equal(60, result.Settings.Behaviour.ProactiveIntervalMinutes);
        }

        [Test]
        public async Task FreeRiding_ThroughTheSettingsService_IsSavedAndAnnounced()
        {
            var service = new SettingsService(_store);
            await service.InitializeAsync();
            var announced = 0;
            service.Changed += (_, _) => announced++;

            await service.UpdateAsync(settings => settings.Behaviour.FreeRiding = true);

            Assert.True(service.Current.Behaviour.FreeRiding);
            Assert.Equal(1, announced);

            // A second copy of the application reading the same file sees the choice.
            using var again = new JsonSettingsStore(_paths);
            Assert.True((await again.LoadAsync()).Settings.Behaviour.FreeRiding);
        }
    }
}
