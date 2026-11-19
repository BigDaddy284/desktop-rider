#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public sealed class SettingsServiceTests
    {
        private TempDirectory _temp = null!;
        private JsonSettingsStore _store = null!;
        private SettingsService _service = null!;

        // NUnit uses one object for every test in the class, so each test is given a fresh folder and service here.
        [SetUp]
        public void SetUp()
        {
            _temp = new TempDirectory();
            _store = new JsonSettingsStore(new AppPaths(_temp.Path));
            _service = new SettingsService(_store);
        }

        [TearDown]
        public void TearDown()
        {
            _store.Dispose();
            _temp.Dispose();
        }

        [Test]
        public void BeforeInitialize_StandardSettingsAreAvailable()
        {
            Assert.Equal(SettingsLoadStatus.Defaults, _service.LoadStatus);
            Assert.True(_service.Current.Appearance.AlwaysOnTop);
        }

        [Test]
        public async Task Update_AppliesSavesAndAnnouncesTheChange()
        {
            await _service.InitializeAsync();
            var announced = 0;
            _service.Changed += (_, _) => announced++;

            await _service.UpdateAsync(s => s.Appearance.AlwaysOnTop = false);

            Assert.False(_service.Current.Appearance.AlwaysOnTop);
            Assert.Equal(1, announced);

            var reloaded = new SettingsService(_store);
            await reloaded.InitializeAsync();
            Assert.Equal(SettingsLoadStatus.Loaded, reloaded.LoadStatus);
            Assert.False(reloaded.Current.Appearance.AlwaysOnTop);
        }

        [Test]
        public async Task Update_KeepsValuesInRange()
        {
            await _service.UpdateAsync(s => s.Appearance.Scale = 1000);

            Assert.Equal(AppearanceSettings.MaxScale, _service.Current.Appearance.Scale);
        }

        [Test]
        public async Task Update_DoesNotAnnounce_WhenSavingFails()
        {
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            var announced = false;
            _service.Changed += (_, _) => announced = true;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.UpdateAsync(_ => { }, cancellation.Token));

            Assert.False(announced);
        }

        [Test]
        public async Task Update_RejectsNull()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.UpdateAsync(null!));
        }
    }
}
