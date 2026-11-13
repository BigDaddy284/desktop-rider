using DesktopRider.Core.ViewModels;

namespace DesktopRider.Core.Tests;

public class TrayViewModelTests
{
    [Fact]
    public void Menu_WhileTheRiderIsShowing_OffersHideAndExit()
    {
        using var context = new Context();

        var menu = context.Tray.BuildMenu();

        Assert.Equal(
            [TrayViewModel.HideText, TrayViewModel.AlwaysOnTopText, TrayViewModel.ResetPositionText, string.Empty, TrayViewModel.ExitText],
            menu.Select(entry => entry.Text));
        Assert.True(menu[3].IsSeparator);
        Assert.DoesNotContain(menu, entry => entry.Text == TrayViewModel.ShowText);
    }

    [Fact]
    public void Menu_WhileTheRiderIsHidden_OffersShowAndExit()
    {
        using var context = new Context(startHidden: true);

        var menu = context.Tray.BuildMenu();

        Assert.Equal(
            [TrayViewModel.ShowText, TrayViewModel.AlwaysOnTopText, string.Empty, TrayViewModel.ExitText],
            menu.Select(entry => entry.Text));
    }

    [Fact]
    public void HideThenShow_FromTheMenu_ChangesTheRiderAndTheMenu()
    {
        using var context = new Context();

        Entry(context, TrayViewModel.HideText).Command!.Execute(null);
        Assert.False(context.Overlay.CharacterVisible);

        Entry(context, TrayViewModel.ShowText).Command!.Execute(null);
        Assert.True(context.Overlay.CharacterVisible);
        Assert.Equal(TrayViewModel.HideText, context.Tray.BuildMenu()[0].Text);
    }

    [Fact]
    public void Exit_FromTheMenu_AsksTheApplicationToClose_EvenWhileHidden()
    {
        using var context = new Context(startHidden: true);
        var exits = 0;
        context.Overlay.ExitRequested += (_, _) => exits++;

        Entry(context, TrayViewModel.ExitText).Command!.Execute(null);

        Assert.Equal(1, exits);
    }

    [Fact]
    public void AlwaysOnTop_ShowsATickThatFollowsTheSetting()
    {
        using var context = new Context();

        Assert.True(Entry(context, TrayViewModel.AlwaysOnTopText).IsChecked);
        Entry(context, TrayViewModel.AlwaysOnTopText).Command!.Execute(null);

        Assert.False(Entry(context, TrayViewModel.AlwaysOnTopText).IsChecked);
        Assert.False(context.Settings.Current.Appearance.AlwaysOnTop);
    }

    [Fact]
    public void MoveBackToTheCorner_FromTheMenu_RaisesTheRequest()
    {
        using var context = new Context();
        var resets = 0;
        context.Overlay.ResetPositionRequested += (_, _) => resets++;

        Entry(context, TrayViewModel.ResetPositionText).Command!.Execute(null);

        Assert.Equal(1, resets);
    }

    [Fact]
    public void ClickingTheIcon_HidesThenShows()
    {
        using var context = new Context();

        context.Tray.DefaultCommand.Execute(null);
        Assert.False(context.Overlay.CharacterVisible);

        context.Tray.DefaultCommand.Execute(null);
        Assert.True(context.Overlay.CharacterVisible);
    }

    [Fact]
    public void ToolTip_SaysWhenTheRiderIsHidden()
    {
        using var context = new Context();

        Assert.Equal("Desktop Rider", context.Tray.ToolTip);
        context.Overlay.HideCommand.Execute(null);
        Assert.Equal("Desktop Rider (hidden)", context.Tray.ToolTip);
        Assert.True(context.Tray.ToolTip.Length <= 63); // The longest text Windows shows on every version.
    }

    [Fact]
    public void FirstHide_ShowsTheReminderOnce_AndRemembersIt()
    {
        using var context = new Context();

        context.Overlay.HideCommand.Execute(null);

        var hint = Assert.Single(context.Hints);
        Assert.Equal(TrayViewModel.HintTitle, hint.Title);
        Assert.Equal(TrayViewModel.HintMessage, hint.Message);
        Assert.True(context.Settings.Current.Application.TrayHintShown);

        context.Overlay.ShowCommand.Execute(null);
        context.Overlay.HideCommand.Execute(null);
        Assert.Single(context.Hints);
    }

    [Fact]
    public void Hide_AfterTheReminderWasShownInAnEarlierSession_ShowsNothing()
    {
        using var context = new Context();
        context.Settings.Current.Application.TrayHintShown = true;

        context.Overlay.HideCommand.Execute(null);

        Assert.Empty(context.Hints);
    }

    [Fact]
    public void OtherSettingsChanges_DoNotShowTheReminder()
    {
        using var context = new Context();

        context.Overlay.ToggleAlwaysOnTopCommand.Execute(null);
        context.Overlay.ShowCommand.Execute(null);

        Assert.Empty(context.Hints);
    }

    [Fact]
    public void FirstHide_WhenSavingFails_StillShowsTheReminderOnlyOnce()
    {
        using var context = new Context();
        context.Settings.FailSaves = true;

        context.Overlay.HideCommand.Execute(null);
        context.Overlay.ShowCommand.Execute(null);
        context.Overlay.HideCommand.Execute(null);

        Assert.Single(context.Hints);
    }

    [Fact]
    public void StartingHidden_AlwaysShowsTheReminder()
    {
        using var context = new Context(startHidden: true);
        context.Settings.Current.Application.TrayHintShown = true;

        context.Tray.AnnounceIfStartedHidden();

        Assert.Single(context.Hints);
    }

    [Fact]
    public void StartingVisible_ShowsNoReminder()
    {
        using var context = new Context();

        context.Tray.AnnounceIfStartedHidden();

        Assert.Empty(context.Hints);
    }

    [Fact]
    public void ClickingTheReminder_BringsTheRiderBack()
    {
        using var context = new Context(startHidden: true);

        context.Tray.HintClickedCommand.Execute(null);

        Assert.True(context.Overlay.CharacterVisible);
    }

    [Fact]
    public void AfterDispose_NoMoreReminders()
    {
        using var context = new Context();
        context.Tray.Dispose();

        context.Overlay.HideCommand.Execute(null);

        Assert.Empty(context.Hints);
    }

    private static TrayMenuEntry Entry(Context context, string text) => context.Tray.BuildMenu().Single(entry => entry.Text == text);

    [Fact]
    public void Contributors_AddTheirLinesBetweenTheRidersAndExit()
    {
        var contributor = new FakeContributor();
        using var context = new Context(contributors: [contributor, new FakeContributor { Entries = [] }]);

        var menu = context.Tray.BuildMenu();

        Assert.Equal(
            [TrayViewModel.HideText, TrayViewModel.AlwaysOnTopText, TrayViewModel.ResetPositionText, string.Empty, "Extra", "More", string.Empty, TrayViewModel.ExitText],
            menu.Select(entry => entry.Text));
        Assert.False(menu[5].IsSeparator);
        Assert.Equal(["Choice"], menu[5].Children!.Select(entry => entry.Text));
    }

    private sealed class FakeContributor : ITrayMenuContributor
    {
        public IReadOnlyList<TrayMenuEntry> Entries { get; set; } =
        [
            new("Extra", new RelayCommand(() => { })),
            new("More", null, Children: [new("Choice", new RelayCommand(() => { }), IsChecked: true)]),
        ];

        public IReadOnlyList<TrayMenuEntry> BuildEntries() => Entries;
    }

    private sealed class Context : IDisposable
    {
        public Context(bool startHidden = false, IEnumerable<ITrayMenuContributor>? contributors = null)
        {
            Settings.Current.Appearance.CharacterVisible = !startHidden;
            Overlay = new OverlayViewModel(Settings);
            Tray = new TrayViewModel(Overlay, Settings, contributors);
            Tray.HintRequested += (_, hint) => Hints.Add(hint);
        }

        public FakeSettingsService Settings { get; } = new();

        public OverlayViewModel Overlay { get; }

        public TrayViewModel Tray { get; }

        public List<TrayHintEventArgs> Hints { get; } = [];

        public void Dispose()
        {
            Tray.Dispose();
            Overlay.Dispose();
        }
    }
}
