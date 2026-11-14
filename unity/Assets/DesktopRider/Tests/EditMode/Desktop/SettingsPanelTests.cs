#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Desktop;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class SettingsPanelTests
    {
        private sealed class FakeRegistry : IStartupRegistry
        {
            public Dictionary<string, string> Values { get; } = new();
            public string? Read(string name) => Values.TryGetValue(name, out var v) ? v : null;
            public void Write(string name, string command) => Values[name] = command;
            public void Remove(string name) => Values.Remove(name);
        }

        private static (SettingsPanel Panel, FakeSettingsService Settings, FakeRegistry Registry) Make()
        {
            var settings = new FakeSettingsService();
            var registry = new FakeRegistry();
            var startup = new StartWithWindows(settings, registry, @"C:\Desktop Rider\DesktopRider3D.exe");
            return (new SettingsPanel(settings, startup), settings, registry);
        }

        [Test]
        public void Shows_the_settings_in_three_groups_and_nothing_about_the_chat()
        {
            var (panel, _, _) = Make();
            Assert.Equal(new[] { SettingsPanel.RiderSection, SettingsPanel.BehaviourSection, SettingsPanel.WindowsSection }, panel.Sections);
            Assert.Equal(new[] { "size", "speed", "roam", "tricks", "idle", "ontop", "startup" }, panel.Rows.Select(r => r.Id));
            Assert.All(panel.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Description)));
            Assert.All(panel.Rows, r => Assert.DoesNotContain("chat", r.Title + r.Description, StringComparison.OrdinalIgnoreCase));
            Assert.All(panel.Rows, r => Assert.DoesNotContain("Ollama", r.Title + r.Description, StringComparison.OrdinalIgnoreCase));
        }

        [Test]
        public void Without_the_startup_handling_the_start_with_windows_line_is_left_out()
        {
            var panel = new SettingsPanel(new FakeSettingsService());
            Assert.DoesNotContain(panel.Rows, r => r.Id == "startup");
        }

        [Test]
        public void The_tray_menu_gets_a_settings_line_that_asks_for_the_window()
        {
            var (panel, _, _) = Make();
            int asked = 0;
            panel.Requested += (s, e) => asked++;
            var line = Assert.Single(panel.BuildEntries());
            Assert.Equal(SettingsPanel.MenuText, line.Text);
            line.Command!.Execute(null);
            Assert.Equal(1, asked);
        }

        [Test]
        public async Task Each_line_reads_and_changes_its_own_setting()
        {
            var (panel, settings, registry) = Make();

            await panel["size"].SetAsync(1.5);
            Assert.Equal(1.5, settings.Current.Appearance.Scale);
            await panel["size"].SetAsync(9);
            Assert.Equal(AppearanceSettings.MaxScale, settings.Current.Appearance.Scale);
            await panel["speed"].SetAsync(0.1);
            Assert.Equal(AppearanceSettings.MinAnimationSpeed, settings.Current.Appearance.AnimationSpeed);

            Assert.False(panel["roam"].IsOn);
            await panel["roam"].SetAsync(1);
            Assert.True(settings.Current.Behaviour.FreeRiding);
            Assert.True(panel["roam"].IsOn);

            Assert.Equal(1.0, panel["tricks"].Value);
            Assert.Equal(new[] { "Never", "Sometimes", "Often" }, panel["tricks"].Choices);
            await panel["tricks"].SetAsync(2);
            Assert.Equal(TrickFrequency.Often, settings.Current.Behaviour.TricksByHimself);
            await panel["tricks"].SetAsync(7);
            Assert.Equal(TrickFrequency.Often, settings.Current.Behaviour.TricksByHimself);
            await panel["tricks"].SetAsync(0);
            Assert.Equal(TrickFrequency.Never, settings.Current.Behaviour.TricksByHimself);

            await panel["idle"].SetAsync(0);
            Assert.False(settings.Current.Behaviour.IdleAnimations);
            await panel["ontop"].SetAsync(0);
            Assert.False(settings.Current.Appearance.AlwaysOnTop);

            await panel["startup"].SetAsync(1);
            Assert.True(settings.Current.Application.StartWithWindows);
            Assert.Single(registry.Values);
            await panel["startup"].SetAsync(0);
            Assert.Empty(registry.Values);
        }

        [Test]
        public async Task A_change_that_cannot_be_saved_still_applies()
        {
            var (panel, settings, _) = Make();
            settings.FailSaves = true;
            await panel["roam"].SetAsync(1);
            Assert.True(settings.Current.Behaviour.FreeRiding);
        }

        [Test]
        public void Layout_puts_every_line_inside_the_window_without_overlaps()
        {
            var (panel, _, _) = Make();
            var layout = new SettingsLayout(panel);
            var areas = panel.Rows.Select(layout.RowArea).Concat(layout.Headings.Select(h => h.Area)).OrderBy(a => a.Y).ToList();
            for (int i = 1; i < areas.Count; i++) Assert.True(areas[i].Y >= areas[i - 1].Bottom - 0.001, "overlap at " + areas[i]);
            foreach (var row in panel.Rows)
            {
                var c = layout.Control(row);
                var t = layout.TextArea(row);
                Assert.True(c.X >= 0 && c.Right <= SettingsLayout.Width - SettingsLayout.Margin + 0.001, row.Id);
                Assert.True(t.Right <= c.X, row.Id + ": the words run into the control");
                Assert.True(t.Width >= 200, row.Id + ": too little room for the words");
                Assert.True(c.Y >= layout.RowArea(row).Y && c.Bottom <= layout.RowArea(row).Bottom, row.Id);
            }
            Assert.True(layout.Close.Bottom <= layout.Height && layout.Reset.Right < layout.Close.X);
        }

        [Test]
        public void Hit_testing_finds_switches_choices_sliders_and_buttons()
        {
            var (panel, _, _) = Make();
            var layout = new SettingsLayout(panel);

            var roam = layout.RowArea(panel["roam"]);
            Assert.Same(panel["roam"], layout.HitTest(roam.X + 5, roam.Y + 20).Row);       // the words count for a switch

            var often = layout.ChoiceButton(panel["tricks"], 2);
            var hit = layout.HitTest(often.X + 10, often.Y + 10);
            Assert.Same(panel["tricks"], hit.Row);
            Assert.Equal(2, hit.Choice);
            Assert.True(layout.HitTest(layout.TextArea(panel["tricks"]).X + 5, layout.TextArea(panel["tricks"]).Y + 5).IsEmpty);

            var size = layout.Control(panel["size"]);
            Assert.Same(panel["size"], layout.HitTest(size.X + 50, size.Y + 10).Row);

            Assert.Equal(SettingsLayout.CloseButton, layout.HitTest(layout.Close.X + 5, layout.Close.Y + 5).Button);
            Assert.Equal(SettingsLayout.ResetButton, layout.HitTest(layout.Reset.X + 5, layout.Reset.Y + 5).Button);
            Assert.True(layout.HitTest(2, 2).IsEmpty);
        }

        [Test]
        public async Task A_slider_follows_the_mouse_and_stays_within_its_ends()
        {
            var (panel, _, _) = Make();
            var layout = new SettingsLayout(panel);
            var row = panel["size"];
            var c = layout.Control(row);
            Assert.Equal(row.Min, layout.SliderValueAt(row, c.X - 100));
            Assert.Equal(row.Max, layout.SliderValueAt(row, c.Right + 100));
            Assert.Equal((row.Min + row.Max) / 2, layout.SliderValueAt(row, c.X + (c.Width / 2)), 6);
            await row.SetAsync(row.Max);
            Assert.Equal(1.0, SettingsLayout.SliderFraction(row));
        }
    }
}
