#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DesktopRider.Core.Desktop;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class StartWithWindowsTests
    {
        private const string Exe = @"C:\Program Files\Desktop Rider\DesktopRider3D.exe";
        private const string Command = "\"" + Exe + "\"";

        private sealed class FakeRegistry : IStartupRegistry
        {
            public Dictionary<string, string> Values { get; } = new();

            public bool Refuse { get; set; }

            public int Writes { get; private set; }

            public string? Read(string name) => Values.TryGetValue(name, out var v) ? v : null;

            public void Write(string name, string command)
            {
                if (Refuse) throw new UnauthorizedAccessException("Policy says no.");
                Writes++;
                Values[name] = command;
            }

            public void Remove(string name)
            {
                if (Refuse) throw new UnauthorizedAccessException("Policy says no.");
                Values.Remove(name);
            }
        }

        [Test]
        public void CommandFor_QuotesThePath_OnlyOnce()
        {
            Assert.Equal(Command, StartWithWindows.CommandFor(Exe));
            Assert.Equal(Command, StartWithWindows.CommandFor(Command));
        }

        [Test]
        public void IsOffByDefault_AndApplyWritesNothing()
        {
            var settings = new FakeSettingsService();
            var registry = new FakeRegistry();
            var startup = new StartWithWindows(settings, registry, Exe);

            startup.Apply();

            Assert.False(startup.Enabled);
            Assert.Empty(registry.Values);
            var line = Assert.Single(startup.BuildEntries());
            Assert.Equal(StartWithWindows.MenuText, line.Text);
            Assert.False(line.IsChecked);
        }

        [Test]
        public async Task Ticking_AddsTheLine_AndRemembersIt()
        {
            var settings = new FakeSettingsService();
            var registry = new FakeRegistry();
            var startup = new StartWithWindows(settings, registry, Exe);

            await startup.SetAsync(true);

            Assert.Equal(Command, registry.Values[StartWithWindows.EntryName]);
            Assert.True(settings.Current.Application.StartWithWindows);
            Assert.True(Assert.Single(startup.BuildEntries()).IsChecked);
        }

        [Test]
        public async Task Unticking_RemovesTheLine()
        {
            var settings = new FakeSettingsService();
            var registry = new FakeRegistry();
            var startup = new StartWithWindows(settings, registry, Exe);
            await startup.SetAsync(true);

            await startup.SetAsync(false);

            Assert.Empty(registry.Values);
            Assert.False(settings.Current.Application.StartWithWindows);
        }

        [Test]
        public void TheMenuLine_TogglesIt()
        {
            var settings = new FakeSettingsService();
            var registry = new FakeRegistry();
            var startup = new StartWithWindows(settings, registry, Exe);

            Assert.Single(startup.BuildEntries()).Command!.Execute(null);

            Assert.True(settings.Current.Application.StartWithWindows);
            Assert.True(registry.Values.ContainsKey(StartWithWindows.EntryName));
        }

        [Test]
        public void Apply_FixesTheStoredPath_AfterTheProgramMoves()
        {
            var settings = new FakeSettingsService();
            settings.Current.Application.StartWithWindows = true;
            var registry = new FakeRegistry();
            registry.Values[StartWithWindows.EntryName] = "\"D:\\Old place\\DesktopRider3D.exe\"";

            new StartWithWindows(settings, registry, Exe).Apply();

            Assert.Equal(Command, registry.Values[StartWithWindows.EntryName]);
        }

        [Test]
        public void Apply_DoesNotRewriteALineThatIsAlreadyRight()
        {
            var settings = new FakeSettingsService();
            settings.Current.Application.StartWithWindows = true;
            var registry = new FakeRegistry();
            registry.Values[StartWithWindows.EntryName] = Command;

            new StartWithWindows(settings, registry, Exe).Apply();

            Assert.Equal(0, registry.Writes);
        }

        [Test]
        public void Apply_LeavesOtherProgramsAlone()
        {
            var settings = new FakeSettingsService();
            var registry = new FakeRegistry();
            registry.Values["Something else"] = "\"C:\\x.exe\"";

            new StartWithWindows(settings, registry, Exe).Apply();

            Assert.Equal("\"C:\\x.exe\"", registry.Values["Something else"]);
        }

        [Test]
        public async Task WhenWindowsRefuses_TheSettingStaysOff()
        {
            var settings = new FakeSettingsService();
            var registry = new FakeRegistry { Refuse = true };
            var startup = new StartWithWindows(settings, registry, Exe);

            await startup.SetAsync(true);

            Assert.False(settings.Current.Application.StartWithWindows);
            Assert.Equal(0, settings.SaveCount);
        }

        [Test]
        public async Task WhenSavingFails_TheChoiceStillApplies()
        {
            var settings = new FakeSettingsService { FailSaves = true };
            var registry = new FakeRegistry();
            var startup = new StartWithWindows(settings, registry, Exe);

            await startup.SetAsync(true);

            Assert.True(registry.Values.ContainsKey(StartWithWindows.EntryName));
        }

        [Test]
        public void AMissingPath_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new StartWithWindows(new FakeSettingsService(), new FakeRegistry(), " "));
        }
    }
}
