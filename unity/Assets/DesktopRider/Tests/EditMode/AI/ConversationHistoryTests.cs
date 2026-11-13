#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Context = DesktopRider.AI.Tests.ConversationViewModelTests.Context;
using DesktopRider.Core.Infrastructure;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    /// <summary>The conversation being kept on this computer between sessions.</summary>
    public class ConversationHistoryTests
    {
        [Test]
        public async Task AfterAnExchange_BothLinesAreKept_WithWhenTheyWereSaid()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Braaap!");
            context.Chat.Draft = "Hello";

            await context.Chat.SendAsync();

            Assert.Equal(
                new[] { new SavedLine(Speaker.User, "Hello", context.Clock.Now), new SavedLine(Speaker.Rider, "Braaap!", context.Clock.Now) },
                context.Store.Saved);
        }

        [Test]
        public async Task TheUsersMessage_IsKeptBeforeTheAnswerArrives()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "one ", "two" });
            context.Provider.Gate = new TaskCompletionSource();
            context.Chat.Draft = "Hello";

            var sending = context.Chat.SendAsync();
            await context.Provider.FirstPieceSent.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(new[] { Speaker.User }, context.Store.Saved.Select(line => line.From));

            context.Provider.Gate.SetResult();
            await sending;
            Assert.Equal(new[] { "Hello", "one two" }, context.Store.Saved.Select(line => line.Text));
        }

        [Test]
        public async Task ExplanationsFromDesktopRider_AreNotKept()
        {
            using var context = new Context();
            context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Ollama is not running.");
            context.Chat.Draft = "Hello";

            await context.Chat.SendAsync();

            Assert.Contains(context.Chat.Lines, line => line.Speaker == Speaker.Notice);
            Assert.Equal(new[] { Speaker.User }, context.Store.Saved.Select(line => line.From));
        }

        [Test]
        public async Task Restore_BringsBackLastTimesConversation_AndTheRiderRemembersIt()
        {
            using var context = new Context();
            var yesterday = context.Clock.Now.AddDays(-1);
            context.Store.Saved = new List<SavedLine> { new(Speaker.User, "My bike is a 250.", yesterday), new(Speaker.Rider, "Nice!", yesterday) };

            await context.Chat.RestoreAsync();

            Assert.Equal(
                new[] { (Speaker.User, "My bike is a 250."), (Speaker.Rider, "Nice!") },
                context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
            Assert.Equal(ConversationState.Idle, context.Chat.State);
            Assert.Empty(context.Provider.Requests);

            context.Provider.Reply.Add("A 250.");
            context.Chat.Draft = "What is my bike?";
            await context.Chat.SendAsync();

            Assert.Equal(
                new[] { "My bike is a 250.", "Nice!", "What is my bike?" },
                context.Provider.Requests[0].Messages.Skip(1).Select(message => message.Content));
        }

        [Test]
        public async Task Restore_LeavesOutLinesOlderThanTheUserChoseToKeep()
        {
            using var context = new Context();
            context.Settings.Current.Memory.RetentionDays = 7;
            context.Store.Saved =
            new List<SavedLine>
            {
                new(Speaker.User, "old", context.Clock.Now.AddDays(-8)),
                new(Speaker.Rider, "recent", context.Clock.Now.AddDays(-6)),
            };

            await context.Chat.RestoreAsync();

            Assert.Equal(new[] { "recent" }, context.Chat.Lines.Select(line => line.Text));
        }

        [Test]
        public async Task Restore_KeepsNoMoreThanTheMostLinesShown()
        {
            using var context = new Context();
            context.Store.Saved = (Enumerable.Range(0, ConversationViewModel.MaxLinesKept + 20).Select(n => new SavedLine(Speaker.User, $"line {n}", context.Clock.Now))).ToList();

            await context.Chat.RestoreAsync();

            Assert.Equal(ConversationViewModel.MaxLinesKept, context.Chat.Lines.Count);
            Assert.Equal("line 20", context.Chat.Lines[0].Text);
        }

        [Test]
        public async Task WithKeepingSwitchedOff_NothingIsRestoredOrSaved()
        {
            using var context = new Context();
            context.Settings.Current.Memory.KeepConversationHistory = false;
            context.Store.Saved = new List<SavedLine> { new(Speaker.User, "from before", context.Clock.Now) };
            context.Provider.Reply.Add("Hi");

            await context.Chat.RestoreAsync();
            context.Chat.Draft = "Hello";
            await context.Chat.SendAsync();

            Assert.Equal(0, context.Store.LoadCount);
            Assert.Equal(0, context.Store.SaveCount);
            Assert.Equal(new[] { "Hello", "Hi" }, context.Chat.Lines.Select(line => line.Text));
        }

        [Test]
        public async Task SavedLinesOlderThanTheUserChoseToKeep_AreDroppedAtTheNextSave()
        {
            using var context = new Context();
            context.Settings.Current.Memory.RetentionDays = 7;
            context.Provider.Reply.Add("first answer");
            context.Chat.Draft = "first";
            await context.Chat.SendAsync();

            context.Clock.Advance(TimeSpan.FromDays(8));
            context.Provider.Reply.Clear();
            context.Provider.Reply.Add("second answer");
            context.Chat.Draft = "second";
            await context.Chat.SendAsync();

            Assert.Equal(new[] { "second", "second answer" }, context.Store.Saved.Select(line => line.Text));
            // The older lines stay on screen until Desktop Rider is closed; they are just no longer kept.
            Assert.Equal(4, context.Chat.Lines.Count);
        }

        [Test]
        public async Task Clear_AlsoRemovesTheSavedCopy()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Hi");
            context.Chat.Draft = "Hello";
            await context.Chat.SendAsync();

            await context.Chat.ClearAsync();

            Assert.Empty(context.Chat.Lines);
            Assert.Empty(context.Store.Saved);
            Assert.Equal(1, context.Store.DeleteCount);
        }

        [Test]
        public async Task SwitchingKeepingOff_RemovesTheSavedCopy_AndSwitchingItOnKeepsWhatIsOnScreen()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Hi");
            context.Chat.Draft = "Hello";
            await context.Chat.SendAsync();

            context.Settings.Current.Memory.KeepConversationHistory = false;
            await context.Chat.ApplyHistoryChoiceAsync();

            Assert.Empty(context.Store.Saved);
            Assert.Equal(2, context.Chat.Lines.Count);

            context.Settings.Current.Memory.KeepConversationHistory = true;
            await context.Chat.ApplyHistoryChoiceAsync();

            Assert.Equal(new[] { "Hello", "Hi" }, context.Store.Saved.Select(line => line.Text));
        }

        [Test]
        public async Task WhenSavingFails_TheUserIsToldOnce_AndTheConversationCarriesOn()
        {
            using var context = new Context();
            context.Store.FailSaves = true;
            context.Provider.Reply.Add("Hi");

            context.Chat.Draft = "Hello";
            await context.Chat.SendAsync();
            context.Chat.Draft = "Again";
            await context.Chat.SendAsync();

            Assert.Single(context.Chat.Lines, line => line.Text == ConversationViewModel.HistoryNotSavedNotice);
            Assert.Equal(2, context.Chat.Lines.Count(line => line.Speaker == Speaker.Rider));
            Assert.Equal(ConversationState.Idle, context.Chat.State);
        }

        [Test]
        public async Task AStoppedReply_KeepsWhatHadArrived()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "Part one", " part two" });
            context.Provider.Gate = new TaskCompletionSource();
            context.Chat.Draft = "Hello";

            var sending = context.Chat.SendAsync();
            await context.Provider.FirstPieceSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
            context.Chat.Stop();
            await sending;

            Assert.Equal(new[] { "Hello", "Part one" }, context.Store.Saved.Select(line => line.Text));
        }

        [Test]
        public async Task TheFileStore_RoundTrips_AndStartsEmpty()
        {
            using var folder = new TempFolder();
            using var store = new JsonConversationStore(new AppPaths(folder.Path));
            var at = new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.FromHours(1));

            Assert.Empty(await store.LoadAsync());

            await store.SaveAsync(new SavedLine[] { new(Speaker.User, "Hello \"rider\"", at), new(Speaker.Rider, "Braaap!", at.AddSeconds(3)) });
            var loaded = await store.LoadAsync();

            Assert.Equal(new[] { new SavedLine(Speaker.User, "Hello \"rider\"", at), new SavedLine(Speaker.Rider, "Braaap!", at.AddSeconds(3)) }, loaded);
            var text = await File.ReadAllTextAsync(Path.Combine(folder.Path, "conversation.json"));
            Assert.Contains("\"from\": \"user\"", text, StringComparison.Ordinal);
            Assert.Contains("\"from\": \"rider\"", text, StringComparison.Ordinal);
        }

        [Test]
        public async Task TheFileStore_Delete_RemovesTheFile_AndIsHarmlessWhenThereIsNone()
        {
            using var folder = new TempFolder();
            using var store = new JsonConversationStore(new AppPaths(folder.Path));

            await store.DeleteAsync();
            await store.SaveAsync(new SavedLine[] { new(Speaker.User, "Hello", DateTimeOffset.UnixEpoch) });
            await store.DeleteAsync();

            Assert.False(File.Exists(Path.Combine(folder.Path, "conversation.json")));
            Assert.Empty(await store.LoadAsync());
        }

        [Test]
        public async Task TheFileStore_WithADamagedFile_StartsEmpty_AndSetsTheFileAside()
        {
            using var folder = new TempFolder();
            var file = Path.Combine(folder.Path, "conversation.json");
            await File.WriteAllTextAsync(file, "{ this is not a conversation");
            using var store = new JsonConversationStore(new AppPaths(folder.Path), timeProvider: new ManualClock(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));

            Assert.Empty(await store.LoadAsync());

            Assert.False(File.Exists(file));
            Assert.True(File.Exists(Path.Combine(folder.Path, "conversation.damaged-20260102-030405.json")));
        }

        [Test]
        public async Task TheFileStore_IgnoresEmptyLinesAndExplanations_InAFileEditedByHand()
        {
            using var folder = new TempFolder();
            await File.WriteAllTextAsync(
                Path.Combine(folder.Path, "conversation.json"),
                @"{ ""version"": 1, ""lines"": [
  { ""from"": ""user"", ""text"": """", ""at"": ""2026-06-01T12:00:00+00:00"" },
  { ""from"": ""notice"", ""text"": ""Reply stopped."", ""at"": ""2026-06-01T12:00:00+00:00"" },
  { ""from"": ""rider"", ""text"": ""Kept"", ""at"": ""2026-06-01T12:00:00+00:00"" }
] }");
            using var store = new JsonConversationStore(new AppPaths(folder.Path));

            Assert.Equal(new[] { "Kept" }, (await store.LoadAsync()).Select(line => line.Text));
        }
    }

    /// <summary>A unique scratch folder that is removed when the test finishes.</summary>
    internal sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DesktopRider.AI.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A leftover scratch folder must not fail a test.
            }
        }
    }
}
