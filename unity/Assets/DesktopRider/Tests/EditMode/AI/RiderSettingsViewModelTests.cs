#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    public class RiderSettingsViewModelTests
    {
        [Test]
        public void AtFirst_ShowsTheStandardRider_AndSavesNothing()
        {
            using var context = new Context();

            Assert.True(context.View.ToneCheeky);
            Assert.False(context.View.ToneFriendly);
            Assert.False(context.View.ToneSerious);
            Assert.True(context.View.ShortReplies);
            Assert.False(context.View.DetailedReplies);
            Assert.Equal(string.Empty, context.View.UserName);
            Assert.Equal(string.Empty, context.View.OwnPersonality);
            Assert.False(context.View.UsingOwnPersonality);
            Assert.True(context.View.UsingBuiltInPersonality);
            Assert.False(context.View.HasProblem);
            Assert.Equal(0, context.Settings.SaveCount);
        }

        [Test]
        public void ChoosingAWayOfTalking_SavesIt_AndUnselectsTheOthers()
        {
            using var context = new Context();
            var announced = 0;
            context.View.PropertyChanged += (_, _) => announced++;

            // A radio button reports the new choice and the old one being switched off.
            context.View.ToneSerious = true;
            context.View.ToneCheeky = false;

            Assert.Equal(RiderTone.Serious, context.Settings.Current.Ai.Tone);
            Assert.True(context.View.ToneSerious);
            Assert.False(context.View.ToneCheeky);
            Assert.Equal(1, context.Settings.SaveCount);
            Assert.True(announced > 0);
        }

        [Test]
        public void ChoosingTheSameThingAgain_SavesNothing()
        {
            using var context = new Context();

            context.View.ToneCheeky = true;
            context.View.ShortReplies = true;
            context.View.UserName = "  ";
            context.View.OwnPersonality = string.Empty;

            Assert.Equal(0, context.Settings.SaveCount);
        }

        [Test]
        public void ChoosingReplyLength_SavesIt()
        {
            using var context = new Context();

            context.View.DetailedReplies = true;

            Assert.Equal(ReplyLength.Detailed, context.Settings.Current.Ai.ReplyLength);
            Assert.True(context.View.DetailedReplies);
            Assert.False(context.View.ShortReplies);
        }

        [Test]
        public void TheUsersName_IsSaved_AndCanBeRemoved()
        {
            using var context = new Context();

            context.View.UserName = "Sam";
            Assert.Equal("Sam", context.Settings.Current.Ai.UserName);

            context.View.UserName = string.Empty;
            SettingsNormalizer.Normalize(context.Settings.Current);
            Assert.Null(context.Settings.Current.Ai.UserName);
            Assert.Equal(string.Empty, context.View.UserName);
        }

        [Test]
        public void WritingAnOwnDescription_UsesIt_AndTheButtonPutsTheBuiltInRiderBack()
        {
            using var context = new Context();

            context.View.OwnPersonality = "You are a grumpy old race mechanic.";

            Assert.True(context.View.UsingOwnPersonality);
            Assert.False(context.View.UsingBuiltInPersonality);
            Assert.StartsWith("You are a grumpy old race mechanic.", RiderPersonality.Compose(context.Settings.Current.Ai), StringComparison.Ordinal);

            context.View.UseBuiltInPersonalityCommand.Execute(null);
            SettingsNormalizer.Normalize(context.Settings.Current);

            Assert.False(context.View.UsingOwnPersonality);
            Assert.StartsWith(RiderPersonality.DefaultSystemPrompt, RiderPersonality.Compose(context.Settings.Current.Ai), StringComparison.Ordinal);
        }

        [Test]
        public void WhenSavingFails_SaysSo_AndClearsTheMessageOnceSavingWorksAgain()
        {
            using var context = new Context();
            context.Settings.FailSaves = true;

            context.View.ToneFriendly = true;

            Assert.True(context.View.HasProblem);
            Assert.Equal(RiderSettingsViewModel.SaveFailedText, context.View.Problem);

            context.Settings.FailSaves = false;
            context.View.ToneFriendly = true;

            Assert.False(context.View.HasProblem);
            Assert.True(context.View.ToneFriendly);
        }

        [Test]
        public void AChangeMadeElsewhere_IsShown()
        {
            using var context = new Context();
            var announced = 0;
            context.View.PropertyChanged += (_, _) => announced++;

            context.Settings.UpdateAsync(s => s.Ai.Tone = RiderTone.Friendly);

            Assert.True(context.View.ToneFriendly);
            Assert.Equal(1, announced);
        }

        [Test]
        public void AtFirst_ConversationsAreKeptForAMonth()
        {
            using var context = new Context();

            Assert.True(context.View.KeepConversations);
            Assert.True(context.View.KeepOneMonth);
            Assert.False(context.View.KeepOneWeek);
            Assert.False(context.View.KeepThreeMonths);
            Assert.False(context.View.KeepOneYear);
            Assert.Equal(string.Empty, context.View.ConversationMessage);
        }

        [Test]
        public async Task SwitchingOffKeepingConversations_SavesTheChoice_AndRemovesTheSavedCopy()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Hi");
            context.Chat.Draft = "Hello";
            await context.Chat.SendAsync();
            Assert.NotEmpty(context.Store.Saved);

            context.View.KeepConversations = false;

            Assert.False(context.Settings.Current.Memory.KeepConversationHistory);
            Assert.Empty(context.Store.Saved);
            Assert.Equal(2, context.Chat.Lines.Count);

            context.View.KeepConversations = true;

            Assert.Equal(2, context.Store.Saved.Count);
        }
        [TestCase(7)]
        [TestCase(90)]
        [TestCase(365)]
        public void ChoosingHowLongToKeepConversations_SavesIt(int days)
        {
            using var context = new Context();

            switch (days)
            {
                case 7:
                    context.View.KeepOneWeek = true;
                    break;
                case 90:
                    context.View.KeepThreeMonths = true;
                    break;
                default:
                    context.View.KeepOneYear = true;
                    break;
            }

            context.View.KeepOneMonth = false;

            Assert.Equal(days, context.Settings.Current.Memory.RetentionDays);
            Assert.Equal(days == 7, context.View.KeepOneWeek);
            Assert.Equal(days == 90, context.View.KeepThreeMonths);
            Assert.Equal(days == 365, context.View.KeepOneYear);
            Assert.False(context.View.KeepOneMonth);
        }

        [Test]
        public async Task ShorteningHowLongConversationsAreKept_TrimsTheSavedCopyAtOnce()
        {
            using var context = new Context();
            context.Provider.Reply.Add("old answer");
            context.Chat.Draft = "old";
            await context.Chat.SendAsync();
            context.Clock.Advance(TimeSpan.FromDays(10));
            Assert.Equal(2, context.Store.Saved.Count);

            context.View.KeepOneWeek = true;

            Assert.Empty(context.Store.Saved);
        }

        [Test]
        public async Task DeleteTheConversation_ClearsItEverywhere_AndSaysSo()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Hi");
            context.Chat.Draft = "Hello";
            await context.Chat.SendAsync();

            context.View.DeleteConversationCommand.Execute(null);

            Assert.Empty(context.Chat.Lines);
            Assert.Empty(context.Store.Saved);
            Assert.Equal(RiderSettingsViewModel.ConversationDeletedText, context.View.ConversationMessage);
        }

        [Test]
        public void AtFirst_MemoryIsOn_AndEmpty()
        {
            using var context = new Context();

            Assert.True(context.View.RememberThings);
            Assert.Empty(context.View.Notes);
            Assert.False(context.View.HasNotes);
            Assert.True(context.View.HasNoNotes);
            Assert.Equal(string.Empty, context.View.MemoryMessage);
        }

        [Test]
        public async Task AddingSomethingToRemember_ShowsIt_SavesIt_AndEmptiesTheBox()
        {
            using var context = new Context();
            context.View.NewNote = "  My bike is a 250 two-stroke.  ";

            await context.View.AddNoteAsync();

            Assert.Equal(new[] { "My bike is a 250 two-stroke" }, context.View.Notes.Select(note => note.Text));
            Assert.Equal(new[] { "My bike is a 250 two-stroke" }, context.MemoryStore.Saved.Select(note => note.Text));
            Assert.Equal(string.Empty, context.View.NewNote);
            Assert.True(context.View.HasNotes);
            Assert.Equal(string.Empty, context.View.MemoryMessage);
        }

        [Test]
        public async Task AddingTheSameThingTwice_KeepsOne_AndSaysSo()
        {
            using var context = new Context();
            context.View.NewNote = "I race on Sundays";
            await context.View.AddNoteAsync();
            context.View.NewNote = "i race on sundays";

            await context.View.AddNoteAsync();

            Assert.Single(context.View.Notes);
            Assert.Equal(RiderSettingsViewModel.AlreadyRememberedText, context.View.MemoryMessage);
        }

        [Test]
        public async Task AddingNothing_DoesNothing()
        {
            using var context = new Context();
            context.View.NewNote = "   ";

            context.View.AddNoteCommand.Execute(null);
            await Task.Yield();

            Assert.Empty(context.View.Notes);
            Assert.Equal(string.Empty, context.View.MemoryMessage);
        }

        [Test]
        public async Task ForgetOnOneThing_RemovesOnlyThat()
        {
            using var context = new Context();
            foreach (var note in new[] { "one thing", "another thing" })
            {
                context.View.NewNote = note;
                await context.View.AddNoteAsync();
            }

            context.View.Notes[0].ForgetCommand.Execute(null);

            Assert.Equal(new[] { "another thing" }, context.View.Notes.Select(note => note.Text));
            Assert.Equal(new[] { "another thing" }, context.MemoryStore.Saved.Select(note => note.Text));
        }

        [Test]
        public async Task ForgetEverything_EmptiesTheMemory_RemovesTheFile_AndSaysSo()
        {
            using var context = new Context();
            context.View.NewNote = "one thing";
            await context.View.AddNoteAsync();

            context.View.ForgetEverythingCommand.Execute(null);

            Assert.Empty(context.View.Notes);
            Assert.Empty(context.MemoryStore.Saved);
            Assert.Equal(1, context.MemoryStore.DeleteCount);
            Assert.Equal(RiderSettingsViewModel.EverythingForgottenText, context.View.MemoryMessage);
        }

        [Test]
        public async Task SwitchingMemoryOff_KeepsTheNotes_ButTheRiderStopsUsingThem_AndNothingCanBeAdded()
        {
            using var context = new Context();
            context.View.NewNote = "My dog is called Diesel";
            await context.View.AddNoteAsync();

            context.View.RememberThings = false;

            Assert.False(context.Settings.Current.Memory.Enabled);
            Assert.Single(context.View.Notes);
            Assert.Empty(context.Memory.ForConversation());

            context.View.NewNote = "something else";
            await context.View.AddNoteAsync();

            Assert.Single(context.View.Notes);
            Assert.Equal(RiderSettingsViewModel.MemoryOffText, context.View.MemoryMessage);
            Assert.Equal("something else", context.View.NewNote);

            context.View.RememberThings = true;

            Assert.Equal(new[] { "My dog is called Diesel" }, context.Memory.ForConversation());
            Assert.Equal(string.Empty, context.View.MemoryMessage);
        }

        [Test]
        public async Task WhenTheMemoryCannotBeSaved_TheChangeStillShows_AndTheUserIsTold()
        {
            using var context = new Context();
            context.MemoryStore.FailSaves = true;
            context.View.NewNote = "one thing";

            await context.View.AddNoteAsync();

            Assert.Single(context.View.Notes);
            Assert.Equal(RiderSettingsViewModel.MemoryNotSavedText, context.View.MemoryMessage);

            await context.View.ForgetEverythingAsync();

            Assert.Empty(context.View.Notes);
            Assert.Equal(RiderSettingsViewModel.MemoryNotSavedText, context.View.MemoryMessage);
        }

        [Test]
        public async Task SomethingRememberedInConversation_AppearsInTheSettings()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Got it!");
            context.Chat.Draft = "Remember that my race number is 284";

            await context.Chat.SendAsync();

            Assert.Equal(new[] { "my race number is 284" }, context.View.Notes.Select(note => note.Text));
        }

        [Test]
        public void AtFirst_TheRiderNeverSpeaksFirst()
        {
            using var context = new Context();

            Assert.False(context.View.RiderSpeaksFirst);
            Assert.True(context.View.Every30Minutes);
            Assert.False(context.View.Every15Minutes);
            Assert.False(context.View.EveryHour);
            Assert.False(context.View.EveryTwoHours);
            Assert.False(context.View.QuietHoursOn);
            Assert.Equal("22:00", context.View.QuietFrom);
            Assert.Equal("08:00", context.View.QuietUntil);
            Assert.Equal(string.Empty, context.View.QuietHoursMessage);
        }

        [Test]
        public void LettingTheRiderSpeakFirst_AndHowOften_AreSaved()
        {
            using var context = new Context();

            context.View.RiderSpeaksFirst = true;
            context.View.EveryTwoHours = true;
            context.View.Every30Minutes = false;

            Assert.True(context.Settings.Current.Behaviour.ProactiveComments);
            Assert.Equal(120, context.Settings.Current.Behaviour.ProactiveIntervalMinutes);
            Assert.True(context.View.EveryTwoHours);
            Assert.False(context.View.Every30Minutes);

            context.View.Every15Minutes = true;
            Assert.Equal(15, context.Settings.Current.Behaviour.ProactiveIntervalMinutes);
            context.View.EveryHour = true;
            Assert.Equal(60, context.Settings.Current.Behaviour.ProactiveIntervalMinutes);
            context.View.Every30Minutes = true;
            Assert.Equal(30, context.Settings.Current.Behaviour.ProactiveIntervalMinutes);
        }

        [Test]
        public void AtFirst_TheRiderDoesNotRideFreely()
        {
            using var context = new Context();

            Assert.False(context.View.FreeRiding);
            Assert.False(context.Settings.Current.Behaviour.FreeRiding);
        }

        [Test]
        public void FreeRiding_IsSavedTheMomentItIsSwitched_AndCanBeSwitchedOffAgain()
        {
            using var context = new Context();
            var saves = context.Settings.SaveCount;
            var told = new List<string?>();
            context.View.PropertyChanged += (_, e) => told.Add(e.PropertyName);

            context.View.FreeRiding = true;

            Assert.True(context.Settings.Current.Behaviour.FreeRiding);
            Assert.True(context.View.FreeRiding);
            Assert.Equal(saves + 1, context.Settings.SaveCount);
            Assert.NotEmpty(told);

            // Choosing what is already chosen saves nothing.
            context.View.FreeRiding = true;
            Assert.Equal(saves + 1, context.Settings.SaveCount);

            context.View.FreeRiding = false;
            Assert.False(context.Settings.Current.Behaviour.FreeRiding);
            Assert.Equal(saves + 2, context.Settings.SaveCount);
        }

        [Test]
        public void QuietHours_AreSaved_AsTyped()
        {
            using var context = new Context();

            context.View.QuietHoursOn = true;
            context.View.QuietFrom = "21.30";
            context.View.QuietUntil = "7";

            Assert.True(context.Settings.Current.Behaviour.QuietHoursEnabled);
            Assert.Equal(new TimeOnly(21, 30), context.Settings.Current.Behaviour.QuietHoursStart);
            Assert.Equal(new TimeOnly(7, 0), context.Settings.Current.Behaviour.QuietHoursEnd);
            Assert.Equal("21:30", context.View.QuietFrom);
            Assert.Equal("07:00", context.View.QuietUntil);
            Assert.Equal(string.Empty, context.View.QuietHoursMessage);
        }

        [Test]
        public void ATimeThatMakesNoSense_ChangesNothing_AndSaysWhatToType()
        {
            using var context = new Context();
            var saves = context.Settings.SaveCount;

            context.View.QuietFrom = "late";

            Assert.Equal(saves, context.Settings.SaveCount);
            Assert.Equal("22:00", context.View.QuietFrom);
            Assert.Equal(RiderSettingsViewModel.TimeNotUnderstoodText, context.View.QuietHoursMessage);

            context.View.QuietFrom = "23:00";

            Assert.Equal(string.Empty, context.View.QuietHoursMessage);
            Assert.Equal("23:00", context.View.QuietFrom);
        }
        [TestCase("22:00", 22, 0)]
        [TestCase("8:30", 8, 30)]
        [TestCase(" 08.05 ", 8, 5)]
        [TestCase("7", 7, 0)]
        [TestCase("0:00", 0, 0)]
        [TestCase("10pm", 22, 0)]
        [TestCase("10 PM", 22, 0)]
        [TestCase("7:15am", 7, 15)]
        public void ReadTime_UnderstandsTimesAsPeopleTypeThem(string typed, int hour, int minute)
        {
            Assert.Equal(new TimeOnly(hour, minute), RiderSettingsViewModel.ReadTime(typed));
        }
        [TestCase("")]
        [TestCase(null)]
        [TestCase("25:00")]
        [TestCase("12:75")]
        [TestCase("soon")]
        [TestCase("-3")]
        public void ReadTime_RejectsEverythingElse(string? typed)
        {
            Assert.Null(RiderSettingsViewModel.ReadTime(typed));
        }

        private sealed class Context : IDisposable
        {
            public Context()
            {
                Settings.Current.Ai.Model = "picked-model";
                Memory = new RiderMemory(MemoryStore, Settings, Clock);
                Service = new AiChatService(new AiProviderRegistry(new[] { Provider }), Settings, Memory);
                Chat = new ConversationViewModel(Service, Settings, store: Store, timeProvider: Clock, memory: Memory);
                View = new RiderSettingsViewModel(Settings, Chat, Memory);
            }

            public FakeMemoryStore MemoryStore { get; } = new();

            public RiderMemory Memory { get; }

            public FakeSettingsService Settings { get; } = new();

            public FakeProvider Provider { get; } = new();

            public FakeConversationStore Store { get; } = new();

            public ManualClock Clock { get; } = new();

            public AiChatService Service { get; }

            public ConversationViewModel Chat { get; }

            public RiderSettingsViewModel View { get; }

            public void Dispose()
            {
                View.Dispose();
                Chat.Dispose();
                Service.Dispose();
            }
        }
    }
}
