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

    private sealed class Context : IDisposable
    {
        public Context()
        {
            Settings.Current.Ai.Model = "picked-model";
            Service = new AiChatService(new AiProviderRegistry([Provider]), Settings);
            Chat = new ConversationViewModel(Service, Settings, store: Store, timeProvider: Clock);
            View = new RiderSettingsViewModel(Settings, Chat);
        }

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
