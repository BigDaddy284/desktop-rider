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

    private sealed class Context : IDisposable
    {
        public Context() => View = new RiderSettingsViewModel(Settings);

        public FakeSettingsService Settings { get; } = new();

        public RiderSettingsViewModel View { get; }

        public void Dispose() => View.Dispose();
    }
}
