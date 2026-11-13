#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    /// <summary>
    /// Version 2 must pick up where version 1 left off. The files in Data/Version1 were written by the real
    /// version 1 code (release 0.8.1) and are read here by the Unity logic, copied to a temporary folder first
    /// so the originals are never touched.
    /// </summary>
    public class Version1FilesTests
    {
        [Test]
        public async Task Settings_SavedByVersion1_LoadWithEveryValueIntact()
        {
            using var folder = CopyOfVersion1Data();
            using var store = new JsonSettingsStore(new AppPaths(folder.Path));

            var result = await store.LoadAsync();

            Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
            var settings = result.Settings;
            Assert.Equal(1, settings.SchemaVersion);
            Assert.Equal(1.25, settings.Appearance.Scale);
            Assert.Equal(0.85, settings.Appearance.Opacity);
            Assert.Equal(1.5, settings.Appearance.AnimationSpeed);
            Assert.False(settings.Appearance.AlwaysOnTop);
            Assert.True(settings.Appearance.CharacterVisible);
            Assert.True(settings.Appearance.ChatInBubbles);
            Assert.True(settings.Appearance.ClickThrough);
            Assert.Equal(-320.5, settings.Appearance.Position!.X);
            Assert.Equal(48, settings.Appearance.Position.Y);
            Assert.Equal(@"\\.\DISPLAY2", settings.Appearance.Position.Monitor);
            Assert.False(settings.Behaviour.AutonomousMovement);
            Assert.True(settings.Behaviour.IdleAnimations);
            Assert.Equal(AnimationFrequency.Frequent, settings.Behaviour.AnimationFrequency);
            Assert.True(settings.Behaviour.ProactiveComments);
            Assert.Equal(45, settings.Behaviour.ProactiveIntervalMinutes);
            Assert.True(settings.Behaviour.QuietHoursEnabled);
            Assert.Equal(new TimeOnly(21, 30), settings.Behaviour.QuietHoursStart);
            Assert.Equal(new TimeOnly(7, 15), settings.Behaviour.QuietHoursEnd);
            Assert.Equal(AiSettings.OllamaProvider, settings.Ai.Provider);
            Assert.Equal("http://192.168.1.20:11434", settings.Ai.OllamaAddress);
            Assert.Equal("llama3.2:3b", settings.Ai.Model);
            Assert.Equal("Be brief & <kind>. Don't shout \u2014 it's rude.", settings.Ai.SystemPrompt);
            Assert.Equal(RiderTone.Serious, settings.Ai.Tone);
            Assert.Equal(ReplyLength.Detailed, settings.Ai.ReplyLength);
            Assert.Equal("Zo\u00EB O'Neil", settings.Ai.UserName);
            Assert.Equal(0.3, settings.Ai.Temperature);
            Assert.Equal(45, settings.Ai.RequestTimeoutSeconds);
            Assert.True(settings.Memory.Enabled);
            Assert.True(settings.Memory.KeepConversationHistory);
            Assert.Equal(7, settings.Memory.RetentionDays);
            Assert.True(settings.Application.StartWithWindows);
            Assert.False(settings.Application.MinimiseToTray);
            Assert.False(settings.Application.CheckForUpdates);
            Assert.True(settings.Application.TrayHintShown);

            // Nothing was set aside as damaged.
            Assert.Null(result.BackupFile);
            Assert.Equal(3, Directory.GetFiles(folder.Path).Length);
        }

        [Test]
        public async Task Settings_SavedByVersion1_LeaveFreeRidingOff()
        {
            using var folder = CopyOfVersion1Data();
            using var store = new JsonSettingsStore(new AppPaths(folder.Path));

            var result = await store.LoadAsync();

            Assert.False(result.Settings.Behaviour.FreeRiding);
        }

        [Test]
        public async Task Memory_SavedByVersion1_LoadsWithItsTextAndTimesIntact()
        {
            using var folder = CopyOfVersion1Data();
            using var store = new JsonMemoryStore(new AppPaths(folder.Path));

            var notes = await store.LoadAsync();

            Assert.Equal(
                new[]
                {
                    new MemoryNote("My bike is a 250 \u2014 it's \"fast\"", new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.FromHours(1))),
                    new MemoryNote("I ride at Fenland on Sundays", new DateTimeOffset(2026, 6, 2, 8, 30, 15, 123, TimeSpan.Zero)),
                },
                notes);

            // The clock's offset is kept too, not just the moment.
            Assert.Equal(TimeSpan.FromHours(1), notes[0].SavedAt.Offset);
        }

        [Test]
        public async Task Conversation_SavedByVersion1_LoadsWithItsTextAndTimesIntact()
        {
            using var folder = CopyOfVersion1Data();
            using var store = new JsonConversationStore(new AppPaths(folder.Path));

            var lines = await store.LoadAsync();

            Assert.Equal(
                new[]
                {
                    new SavedLine(Speaker.User, "Hello \"rider\" \U0001F44B", new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.FromHours(1))),
                    new SavedLine(Speaker.Rider, "Braaap! What's up?", new DateTimeOffset(2026, 6, 1, 12, 0, 3, TimeSpan.FromHours(1))),
                },
                lines);
            Assert.Equal(TimeSpan.FromHours(1), lines[0].At.Offset);
        }

        [Test]
        public async Task SavingAgain_KeepsTheSameNamesAndShapesVersion1Uses()
        {
            using var folder = CopyOfVersion1Data();
            var paths = new AppPaths(folder.Path);
            using var settings = new JsonSettingsStore(paths);
            using var memory = new JsonMemoryStore(paths);
            using var conversation = new JsonConversationStore(paths);

            await settings.SaveAsync((await settings.LoadAsync()).Settings);
            await memory.SaveAsync(await memory.LoadAsync());
            await conversation.SaveAsync(await conversation.LoadAsync());

            var settingsText = await File.ReadAllTextAsync(paths.SettingsFile);
            Assert.Contains("\"schemaVersion\": 1", settingsText, StringComparison.Ordinal);
            Assert.Contains("\"animationFrequency\": \"frequent\"", settingsText, StringComparison.Ordinal);
            Assert.Contains("\"quietHoursStart\": \"21:30:00\"", settingsText, StringComparison.Ordinal);
            Assert.Contains("\"quietHoursEnd\": \"07:15:00\"", settingsText, StringComparison.Ordinal);
            Assert.Contains("\"tone\": \"serious\"", settingsText, StringComparison.Ordinal);
            Assert.Contains("\"replyLength\": \"detailed\"", settingsText, StringComparison.Ordinal);
            Assert.Contains("\"provider\": \"Ollama\"", settingsText, StringComparison.Ordinal);
            Assert.Contains("\"monitor\": \"\\\\\\\\.\\\\DISPLAY2\"", settingsText, StringComparison.Ordinal);
            Assert.Contains("\"freeRiding\": false", settingsText, StringComparison.Ordinal);

            var memoryText = await File.ReadAllTextAsync(paths.MemoryFile);
            Assert.Contains("\"version\": 1", memoryText, StringComparison.Ordinal);
            Assert.Contains("\"savedAt\": \"2026-06-01T12:00:00+01:00\"", memoryText, StringComparison.Ordinal);
            Assert.Contains("\"savedAt\": \"2026-06-02T08:30:15.123+00:00\"", memoryText, StringComparison.Ordinal);

            var conversationText = await File.ReadAllTextAsync(paths.ConversationFile);
            Assert.Contains("\"version\": 1", conversationText, StringComparison.Ordinal);
            Assert.Contains("\"from\": \"user\"", conversationText, StringComparison.Ordinal);
            Assert.Contains("\"from\": \"rider\"", conversationText, StringComparison.Ordinal);
            Assert.Contains("\"at\": \"2026-06-01T12:00:03+01:00\"", conversationText, StringComparison.Ordinal);

            // No file was mistaken for a damaged one and set aside.
            Assert.Equal(
                new[] { "conversation.json", "memory.json", "settings.json" },
                Directory.GetFiles(folder.Path).Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal));
        }

        private static TempFolder CopyOfVersion1Data([CallerFilePath] string thisFile = "")
        {
            var source = Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "Data", "Version1");
            Assert.True(Directory.Exists(source), "The version 1 files are missing: " + source);

            var folder = new TempFolder();
            foreach (var name in new[] { "settings.json", "memory.json", "conversation.json" })
            {
                File.Copy(Path.Combine(source, name), Path.Combine(folder.Path, name));
            }

            return folder;
        }
    }
}
