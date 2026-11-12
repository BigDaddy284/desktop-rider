using DesktopRider.Core.Configuration;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.Core.Tests;

public class OverlayViewModelTests
{
    [Fact]
    public void ReflectsTheCurrentSettings()
    {
        var settings = new FakeSettingsService();
        settings.Current.Appearance.AlwaysOnTop = false;
        settings.Current.Appearance.Opacity = 0.6;
        using var viewModel = new OverlayViewModel(settings);

        Assert.False(viewModel.AlwaysOnTop);
        Assert.Equal(0.6, viewModel.CharacterOpacity);
        Assert.Equal("Desktop Rider", viewModel.Title);
    }

    [Fact]
    public async Task SetAlwaysOnTop_SavesAndTellsTheWindow()
    {
        var settings = new FakeSettingsService();
        using var viewModel = new OverlayViewModel(settings);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        await viewModel.SetAlwaysOnTopAsync(false);

        Assert.False(viewModel.AlwaysOnTop);
        Assert.Equal(1, settings.SaveCount);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void ToggleCommand_FlipsAlwaysOnTop()
    {
        var settings = new FakeSettingsService();
        using var viewModel = new OverlayViewModel(settings);

        Assert.True(viewModel.ToggleAlwaysOnTopCommand.CanExecute(null));
        viewModel.ToggleAlwaysOnTopCommand.Execute(null);
        Assert.False(viewModel.AlwaysOnTop);

        viewModel.ToggleAlwaysOnTopCommand.Execute(null);
        Assert.True(viewModel.AlwaysOnTop);
    }

    [Fact]
    public async Task SetAlwaysOnTop_WhenSavingFails_StillAppliesForThisSession()
    {
        var settings = new FakeSettingsService { FailSaves = true };
        using var viewModel = new OverlayViewModel(settings);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;

        await viewModel.SetAlwaysOnTopAsync(false);

        Assert.False(viewModel.AlwaysOnTop);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void MenuCommands_RaiseTheirRequests()
    {
        using var viewModel = new OverlayViewModel(new FakeSettingsService());
        var reset = 0;
        var exit = 0;
        viewModel.ResetPositionRequested += (_, _) => reset++;
        viewModel.ExitRequested += (_, _) => exit++;

        viewModel.ResetPositionCommand.Execute(null);
        viewModel.ExitCommand.Execute(null);

        Assert.Equal(1, reset);
        Assert.Equal(1, exit);
    }

    [Fact]
    public async Task AfterDispose_SettingsChangesNoLongerNotify()
    {
        var settings = new FakeSettingsService();
        var viewModel = new OverlayViewModel(settings);
        var notifications = 0;
        viewModel.PropertyChanged += (_, _) => notifications++;
        viewModel.Dispose();

        await settings.UpdateAsync(s => s.Appearance.Opacity = 0.5);

        Assert.Equal(0, notifications);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public event EventHandler? Changed;

        public AppSettings Current { get; } = new();

        public SettingsLoadStatus LoadStatus => SettingsLoadStatus.Defaults;

        public bool FailSaves { get; set; }

        public int SaveCount { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Action<AppSettings> change, CancellationToken cancellationToken = default)
        {
            change(Current);
            if (FailSaves)
            {
                return Task.FromException(new IOException("The disk is full."));
            }

            SaveCount++;
            Changed?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }
    }
}
