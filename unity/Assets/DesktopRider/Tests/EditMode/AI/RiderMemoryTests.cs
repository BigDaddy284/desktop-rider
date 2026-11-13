#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Context = DesktopRider.AI.Tests.ConversationViewModelTests.Context;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    public class RiderMemoryTests
    {
        [TestCase("Remember that my bike is a 250.", "my bike is a 250")]
        [TestCase("remember my name is Sam", "my name is Sam")]
        [TestCase("Please remember that I race on Sundays!", "I race on Sundays")]
        [TestCase("Hey rider, can you remember that my dog is called Diesel", "my dog is called Diesel")]
        [TestCase("Don't forget that I prefer metric", "I prefer metric")]
        [TestCase("dont forget I ride a two-stroke", "I ride a two-stroke")]
        [TestCase("Remember: front tyre is 13 psi", "front tyre is 13 psi")]
        [TestCase("Could you please remember   I live\nin Norfolk", "I live in Norfolk")]
        [TestCase("Keep in mind that I am a beginner", "I am a beginner")]
        public void FactToRemember_FindsWhatToRemember(string message, string expected)
        {
            Assert.Equal(expected, RiderMemory.FactToRemember(message));
        }
        [TestCase("Do you remember my name?")]
        [TestCase("Remember when we talked about tyres")]
        [TestCase("remember that?")]
        [TestCase("Remember to check the chain")]
        [TestCase("remember me")]
        [TestCase("Remember what I said")]
        [TestCase("What do you remember about me")]
        [TestCase("I can't remember that my bike had a kickstart")]
        [TestCase("remember")]
        [TestCase("remember it")]
        [TestCase("Remember that time at the track")]
        [TestCase("Tell me a joke")]
        [TestCase("")]
        public void FactToRemember_IgnoresQuestionsRemindersAndEverythingElse(string message)
        {
            Assert.Null(RiderMemory.FactToRemember(message));
        }

        [Test]
        public async Task Remember_KeepsTheNote_SavesIt_AndTellsTheAi()
        {
            using var context = new Context();
            var changes = 0;
            context.Memory.Changed += (_, _) => changes++;

            var result = await context.Memory.RememberAsync("  My bike   is a 250.  ");

            Assert.Equal(RememberResult.Remembered, result);
            Assert.Equal(new[] { new MemoryNote("My bike is a 250", context.Clock.Now) }, context.Memory.Notes);
            Assert.Equal(context.Memory.Notes, context.MemoryStore.Saved);
            Assert.Equal(new[] { "My bike is a 250" }, context.Memory.ForConversation());
            Assert.Equal(1, changes);
        }

        [Test]
        public async Task Remember_RefusesNothing_Duplicates_AndMoreThanItCanHold()
        {
            using var context = new Context();

            Assert.Equal(RememberResult.Nothing, await context.Memory.RememberAsync("  . "));
            Assert.Equal(RememberResult.Remembered, await context.Memory.RememberAsync("note 0"));
            Assert.Equal(RememberResult.AlreadyRemembered, await context.Memory.RememberAsync("NOTE 0."));
            for (var n = 1; n < RiderMemory.MaxNotes; n++)
            {
                Assert.Equal(RememberResult.Remembered, await context.Memory.RememberAsync($"note {n}"));
            }

            Assert.Equal(RememberResult.Full, await context.Memory.RememberAsync("one too many"));
            Assert.Equal(RiderMemory.MaxNotes, context.Memory.Notes.Count);
        }

        [Test]
        public async Task Remember_ShortensAVeryLongNote()
        {
            using var context = new Context();

            await context.Memory.RememberAsync(new string('x', 500));

            Assert.Equal(RiderMemory.MaxNoteLength, context.Memory.Notes[0].Text.Length);
        }

        [Test]
        public async Task WithMemorySwitchedOff_NothingIsAdded_AndTheAiIsToldNothing()
        {
            using var context = new Context();
            await context.Memory.RememberAsync("My bike is a 250");
            context.Settings.Current.Memory.Enabled = false;

            Assert.Equal(RememberResult.SwitchedOff, await context.Memory.RememberAsync("something new"));
            Assert.Empty(context.Memory.ForConversation());
            Assert.Single(context.Memory.Notes);
        }

        [Test]
        public async Task Load_BringsBackWhatWasSaved_UpToWhatItCanHold()
        {
            using var context = new Context();
            context.MemoryStore.Saved = (Enumerable.Range(0, RiderMemory.MaxNotes + 5).Select(n => new MemoryNote($"note {n}", context.Clock.Now))).ToList();

            await context.Memory.LoadAsync();

            Assert.Equal(RiderMemory.MaxNotes, context.Memory.Notes.Count);
            Assert.Equal("note 5", context.Memory.Notes[0].Text);
        }

        [Test]
        public async Task Forget_RemovesOneNote_AndForgetEverythingRemovesTheFile()
        {
            using var context = new Context();
            await context.Memory.RememberAsync("one");
            await context.Memory.RememberAsync("two");

            Assert.True(await context.Memory.ForgetAsync(context.Memory.Notes[0]));
            Assert.Equal(new[] { "two" }, context.MemoryStore.Saved.Select(note => note.Text));

            Assert.True(await context.Memory.ForgetEverythingAsync());
            Assert.Empty(context.Memory.Notes);
            Assert.Equal(1, context.MemoryStore.DeleteCount);
        }

        [Test]
        public async Task WhenSavingFails_TheNoteHoldsForNow_AndTheResultSaysSo()
        {
            using var context = new Context();
            context.MemoryStore.FailSaves = true;

            Assert.Equal(RememberResult.NotSaved, await context.Memory.RememberAsync("one"));
            Assert.Single(context.Memory.Notes);
            Assert.False(await context.Memory.ForgetAsync(context.Memory.Notes[0]));
            Assert.Empty(context.Memory.Notes);
            Assert.False(await context.Memory.ForgetEverythingAsync());
        }

        [Test]
        public async Task SayingRememberThat_InConversation_KeepsIt_ConfirmsIt_AndTheAnswerAlreadyKnowsIt()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Got it!");
            var notices = 0;
            context.Chat.NoticeAdded += (_, _) => notices++;
            context.Chat.Draft = "Remember that my bike is a 250";

            await context.Chat.SendAsync();

            Assert.Equal(new[] { "my bike is a 250" }, context.Memory.Notes.Select(note => note.Text));
            Assert.Equal(
                new[] { (Speaker.User, "Remember that my bike is a 250"), (Speaker.Notice, ConversationViewModel.RememberedNotice), (Speaker.Rider, "Got it!") },
                context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
            var request = Assert.Single(context.Provider.Requests);
            Assert.Contains("my bike is a 250", request.Messages[0].Content, StringComparison.Ordinal);
            Assert.Equal(ChatRole.System, request.Messages[0].Role);

            // The confirmation is in the conversation, not the speech bubble, and is never sent to the AI or kept.
            Assert.Equal(0, notices);
            Assert.DoesNotContain(request.Messages, message => message.Content == ConversationViewModel.RememberedNotice);
            Assert.DoesNotContain(context.Store.Saved, line => line.Text == ConversationViewModel.RememberedNotice);
        }

        [Test]
        public async Task AnOrdinaryMessage_AddsNothingToMemory()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Yes, your bike!");
            context.Chat.Draft = "Do you remember what I ride?";

            await context.Chat.SendAsync();

            Assert.Empty(context.Memory.Notes);
            Assert.DoesNotContain(context.Chat.Lines, line => line.Speaker == Speaker.Notice);
        }
        [TestCase(false, false, ConversationViewModel.MemoryOffNotice)]
        [TestCase(true, true, ConversationViewModel.MemoryNotSavedNotice)]
        public async Task SayingRememberThat_WhenItCannotBeKept_SaysWhy(bool enabled, bool failSaves, string expected)
        {
            using var context = new Context();
            context.Settings.Current.Memory.Enabled = enabled;
            context.MemoryStore.FailSaves = failSaves;
            context.Provider.Reply.Add("OK");
            context.Chat.Draft = "Remember that my bike is a 250";

            await context.Chat.SendAsync();

            Assert.Contains(context.Chat.Lines, line => line.Speaker == Speaker.Notice && line.Text == expected);
            Assert.Contains(context.Chat.Lines, line => line.Speaker == Speaker.Rider);
        }

        [Test]
        public async Task SayingTheSameThingAgain_SaysItIsAlreadyRemembered()
        {
            using var context = new Context();
            context.Provider.Reply.Add("OK");
            await context.Memory.RememberAsync("my bike is a 250");
            context.Chat.Draft = "remember my bike is a 250";

            await context.Chat.SendAsync();

            Assert.Single(context.Memory.Notes);
            Assert.Contains(context.Chat.Lines, line => line.Text == ConversationViewModel.AlreadyRememberedNotice);
        }

        [Test]
        public async Task WhatTheRiderRemembers_GoesToTheAi_OnlyWhileMemoryIsOn()
        {
            using var context = new Context();
            await context.Memory.RememberAsync("My dog is called Diesel");
            context.Provider.Reply.Add("Hi");

            context.Chat.Draft = "Hello";
            await context.Chat.SendAsync();
            context.Settings.Current.Memory.Enabled = false;
            context.Chat.Draft = "Hello again";
            await context.Chat.SendAsync();

            Assert.Contains("My dog is called Diesel", context.Provider.Requests[0].Messages[0].Content, StringComparison.Ordinal);
            Assert.DoesNotContain("Diesel", context.Provider.Requests[1].Messages[0].Content, StringComparison.Ordinal);
            Assert.Equal(RiderPersonality.Compose(new AiSettings()), context.Provider.Requests[1].Messages[0].Content);
        }

        [Test]
        public async Task TheFileStore_RoundTrips_Deletes_AndSurvivesADamagedFile()
        {
            using var folder = new TempFolder();
            var file = Path.Combine(folder.Path, "memory.json");
            using var store = new JsonMemoryStore(new AppPaths(folder.Path), timeProvider: new ManualClock(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
            var note = new MemoryNote("My bike is a 250", new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

            Assert.Empty(await store.LoadAsync());
            await store.SaveAsync(new[] { note });
            Assert.Equal(new[] { note }, await store.LoadAsync());

            await store.DeleteAsync();
            Assert.False(File.Exists(file));

            await File.WriteAllTextAsync(file, "not a memory");
            Assert.Empty(await store.LoadAsync());
            Assert.True(File.Exists(Path.Combine(folder.Path, "memory.damaged-20260102-030405.json")));
        }

        [Test]
        public async Task TheFileStore_IgnoresEmptyNotes_InAFileEditedByHand()
        {
            using var folder = new TempFolder();
            await File.WriteAllTextAsync(
                Path.Combine(folder.Path, "memory.json"),
                "{ \"notes\": [ null, { \"text\": \" \", \"savedAt\": \"2026-06-01T12:00:00+00:00\" }, { \"text\": \"Kept\", \"savedAt\": \"2026-06-01T12:00:00+00:00\" } ] }");
            using var store = new JsonMemoryStore(new AppPaths(folder.Path));

            Assert.Equal(new[] { "Kept" }, (await store.LoadAsync()).Select(note => note.Text));
        }
    }
}
