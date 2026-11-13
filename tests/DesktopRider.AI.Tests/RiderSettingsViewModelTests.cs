using DesktopRider.Core.Configuration;

namespace DesktopRider.AI.Tests;

public class RiderSettingsViewModelTests
{
    [Fact]
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

    [Fact]
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

    [Fact]
    public void ChoosingTheSameThingAgain_SavesNothing()
    {
        using var context = new Context();

        context.View.ToneCheeky = true;
        context.View.ShortReplies = true;
        context.View.UserName = "  ";
        context.View.OwnPersonality = string.Empty;

        Assert.Equal(0, context.Settings.SaveCount);
    }

    [Fact]
    public void ChoosingReplyLength_SavesIt()
    {
        using var context = new Context();

        context.View.DetailedReplies = true;

        Assert.Equal(ReplyLength.Detailed, context.Settings.Current.Ai.ReplyLength);
        Assert.True(context.View.DetailedReplies);
        Assert.False(context.View.ShortReplies);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void AChangeMadeElsewhere_IsShown()
    {
        using var context = new Context();
        var announced = 0;
        context.View.PropertyChanged += (_, _) => announced++;

        context.Settings.UpdateAsync(s => s.Ai.Tone = RiderTone.Friendly);

        Assert.True(context.View.ToneFriendly);
        Assert.Equal(1, announced);
    }

    [Fact]
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

    [Fact]
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

    [Theory]
    [InlineData(7)]
    [InlineData(90)]
    [InlineData(365)]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public void AtFirst_MemoryIsOn_AndEmpty()
    {
        using var context = new Context();

        Assert.True(context.View.RememberThings);
        Assert.Empty(context.View.Notes);
        Assert.False(context.View.HasNotes);
        Assert.True(context.View.HasNoNotes);
        Assert.Equal(string.Empty, context.View.MemoryMessage);
    }

    [Fact]
    public async Task AddingSomethingToRemember_ShowsIt_SavesIt_AndEmptiesTheBox()
    {
        using var context = new Context();
        context.View.NewNote = "  My bike is a 250 two-stroke.  ";

        await context.View.AddNoteAsync();

        Assert.Equal(["My bike is a 250 two-stroke"], context.View.Notes.Select(note => note.Text));
        Assert.Equal(["My bike is a 250 two-stroke"], context.MemoryStore.Saved.Select(note => note.Text));
        Assert.Equal(string.Empty, context.View.NewNote);
        Assert.True(context.View.HasNotes);
        Assert.Equal(string.Empty, context.View.MemoryMessage);
    }

    [Fact]
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

    [Fact]
    public async Task AddingNothing_DoesNothing()
    {
        using var context = new Context();
        context.View.NewNote = "   ";

        context.View.AddNoteCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(context.View.Notes);
        Assert.Equal(string.Empty, context.View.MemoryMessage);
    }

    [Fact]
    public async Task ForgetOnOneThing_RemovesOnlyThat()
    {
        using var context = new Context();
        foreach (var note in new[] { "one thing", "another thing" })
        {
            context.View.NewNote = note;
            await context.View.AddNoteAsync();
        }

        context.View.Notes[0].ForgetCommand.Execute(null);

        Assert.Equal(["another thing"], context.View.Notes.Select(note => note.Text));
        Assert.Equal(["another thing"], context.MemoryStore.Saved.Select(note => note.Text));
    }

    [Fact]
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

    [Fact]
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

        Assert.Equal(["My dog is called Diesel"], context.Memory.ForConversation());
        Assert.Equal(string.Empty, context.View.MemoryMessage);
    }

    [Fact]
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

    [Fact]
    public async Task SomethingRememberedInConversation_AppearsInTheSettings()
    {
        using var context = new Context();
        context.Provider.Reply.Add("Got it!");
        context.Chat.Draft = "Remember that my race number is 284";

        await context.Chat.SendAsync();

        Assert.Equal(["my race number is 284"], context.View.Notes.Select(note => note.Text));
    }

    private sealed class Context : IDisposable
    {
        public Context()
        {
            Settings.Current.Ai.Model = "picked-model";
            Memory = new RiderMemory(MemoryStore, Settings, Clock);
            Service = new AiChatService(new AiProviderRegistry([Provider]), Settings, Memory);
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
