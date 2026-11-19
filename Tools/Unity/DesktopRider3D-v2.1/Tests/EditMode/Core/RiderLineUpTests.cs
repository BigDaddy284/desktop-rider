#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class RiderLineUpTests
    {
        [Test]
        public void An_old_settings_file_has_just_its_one_rider_out()
        {
            var appearance = new AppearanceSettings { Character = RiderCharacter.Louie };
            Assert.Equal(new[] { RiderCharacter.Louie }, RiderLineUp.Out(appearance));
            Assert.Empty(RiderLineUp.Companions(appearance));
            SettingsNormalizer.Normalize(new AppSettings { Appearance = appearance });
            Assert.Equal(new[] { RiderCharacter.Louie }, appearance.Riders);
        }

        [Test]
        public void Riders_come_out_and_go_home_and_the_last_one_stays()
        {
            var a = new AppearanceSettings();
            Assert.True(RiderLineUp.Toggle(a, RiderCharacter.Louie));
            Assert.Equal(new[] { RiderCharacter.Harvey, RiderCharacter.Louie }, RiderLineUp.Out(a));
            Assert.Equal(new[] { RiderCharacter.Louie }, RiderLineUp.Companions(a));
            Assert.True(RiderLineUp.Toggle(a, RiderCharacter.Sidecar41));
            Assert.Equal(new[] { RiderCharacter.Harvey, RiderCharacter.Sidecar41, RiderCharacter.Louie }, RiderLineUp.Out(a));
            Assert.Equal(RiderCharacter.Harvey, a.Character);

            // Sending home the main window's rider hands the window to the first one still out.
            Assert.True(RiderLineUp.Toggle(a, RiderCharacter.Harvey));
            Assert.Equal(RiderCharacter.Sidecar41, a.Character);
            Assert.Equal(new[] { RiderCharacter.Louie }, RiderLineUp.Companions(a));
            Assert.True(RiderLineUp.Toggle(a, RiderCharacter.Sidecar41));
            Assert.Equal(RiderCharacter.Louie, a.Character);
            Assert.Empty(RiderLineUp.Companions(a));

            Assert.False(RiderLineUp.Toggle(a, RiderCharacter.Louie));
            Assert.Equal(new[] { RiderCharacter.Louie }, RiderLineUp.Out(a));
            Assert.False(RiderLineUp.Toggle(a, (RiderCharacter)42));
        }

        [Test]
        public void Normalize_drops_unknown_and_repeated_riders_and_keeps_button_order()
        {
            var settings = new AppSettings();
            settings.Appearance.Character = RiderCharacter.Sidecar41;
            settings.Appearance.Riders = new List<RiderCharacter> { RiderCharacter.Louie, (RiderCharacter)9, RiderCharacter.Louie };
            SettingsNormalizer.Normalize(settings);
            Assert.Equal(new[] { RiderCharacter.Sidecar41, RiderCharacter.Louie }, settings.Appearance.Riders);
            Assert.Equal(RiderCharacter.Sidecar41, settings.Appearance.Character);
        }

        [Test]
        public void A_companion_window_is_started_with_its_rider_and_the_main_program()
        {
            string line = RiderLineUp.CompanionArguments(RiderCharacter.Louie, 4321);
            Assert.True(RiderLineUp.TryReadCompanion(("DesktopRider3D.exe " + line).Split(' '), out var who, out int host));
            Assert.Equal(RiderCharacter.Louie, who);
            Assert.Equal(4321, host);
            Assert.False(RiderLineUp.TryReadCompanion(new[] { "DesktopRider3D.exe" }, out _, out _));
            Assert.False(RiderLineUp.TryReadCompanion(new[] { "x", "--companion", "Nobody", "--host", "5" }, out _, out _));
            Assert.False(RiderLineUp.TryReadCompanion(new[] { "x", "--companion", "Louie" }, out _, out _));
            Assert.False(RiderLineUp.TryReadCompanion(new[] { "x", "--companion", "7", "--host", "5" }, out _, out _));
        }

        [Test]
        public async Task A_companion_reads_the_settings_but_never_writes_or_moves_the_file()
        {
            using var temp = new TempDirectory();
            var paths = new AppPaths(temp.Path);
            using (var store = new JsonSettingsStore(paths))
            {
                var saved = new AppSettings();
                RiderLineUp.Toggle(saved.Appearance, RiderCharacter.Louie);
                saved.Appearance.Scale = 0.8;
                await store.SaveAsync(saved);
            }

            var service = new SettingsService(new ReadOnlySettingsStore(paths.SettingsFile));
            int changes = 0;
            service.Changed += (s, e) => changes++;
            await service.InitializeAsync();
            Assert.Equal(0.8, service.Current.Appearance.Scale);
            Assert.Equal(new[] { RiderCharacter.Louie }, RiderLineUp.Companions(service.Current.Appearance));

            // A half-written file is ignored: the last settings read stay in use and the file is left where it is.
            string good = File.ReadAllText(paths.SettingsFile);
            File.WriteAllText(paths.SettingsFile, "{ \"Appearance\": ");
            await service.ReloadAsync();
            Assert.Equal(0.8, service.Current.Appearance.Scale);
            Assert.Equal(1, changes);
            Assert.Equal("{ \"Appearance\": ", File.ReadAllText(paths.SettingsFile));
            Assert.Equal(1, Directory.GetFiles(temp.Path).Length);

            // Changing settings in a companion changes nothing on disk.
            File.WriteAllText(paths.SettingsFile, good.Replace("0.8", "0.6"));
            await service.ReloadAsync();
            Assert.Equal(0.6, service.Current.Appearance.Scale);
            await service.UpdateAsync(s => s.Appearance.Scale = 1.0);
            Assert.Equal(good.Replace("0.8", "0.6"), File.ReadAllText(paths.SettingsFile));
        }
    }
}
