#nullable enable

using System.Threading.Tasks;
using DesktopRider.Core.ViewModels;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class OverlayViewModelTests
    {
        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
        public async Task HideAndShow_ChangeVisibilityAndAreRemembered()
        {
            var settings = new FakeSettingsService();
            using var viewModel = new OverlayViewModel(settings);
            var notifications = 0;
            viewModel.PropertyChanged += (_, _) => notifications++;

            Assert.True(viewModel.CharacterVisible);
            viewModel.HideCommand.Execute(null);
            Assert.False(viewModel.CharacterVisible);
            Assert.False(settings.Current.Appearance.CharacterVisible);

            viewModel.ShowCommand.Execute(null);
            Assert.True(viewModel.CharacterVisible);
            Assert.Equal(2, settings.SaveCount);
            Assert.Equal(2, notifications);

            await viewModel.SetCharacterVisibleAsync(false);
            Assert.False(viewModel.CharacterVisible);
        }

        [Test]
        public void ShowWhenAlreadyShowing_AndHideWhenAlreadyHidden_DoNothing()
        {
            var settings = new FakeSettingsService();
            using var viewModel = new OverlayViewModel(settings);

            viewModel.ShowCommand.Execute(null);
            Assert.Equal(0, settings.SaveCount);

            viewModel.HideCommand.Execute(null);
            viewModel.HideCommand.Execute(null);
            Assert.Equal(1, settings.SaveCount);
        }

        [Test]
        public void ToggleVisibility_FlipsEachTime()
        {
            using var viewModel = new OverlayViewModel(new FakeSettingsService());

            viewModel.ToggleVisibilityCommand.Execute(null);
            Assert.False(viewModel.CharacterVisible);
            viewModel.ToggleVisibilityCommand.Execute(null);
            Assert.True(viewModel.CharacterVisible);
        }

        [Test]
        public async Task Hide_WhenSavingFails_StillHidesForThisSession()
        {
            var settings = new FakeSettingsService { FailSaves = true };
            using var viewModel = new OverlayViewModel(settings);
            var notifications = 0;
            viewModel.PropertyChanged += (_, _) => notifications++;

            await viewModel.SetCharacterVisibleAsync(false);

            Assert.False(viewModel.CharacterVisible);
            Assert.Equal(1, notifications);
        }

        [Test]
        public async Task ChatInBubbles_IsOffAtFirst_AndTheChoiceIsSavedOnce()
        {
            var settings = new FakeSettingsService();
            using var viewModel = new OverlayViewModel(settings);
            Assert.False(viewModel.ChatInBubbles);

            await viewModel.SetChatInBubblesAsync(true);
            await viewModel.SetChatInBubblesAsync(true);

            Assert.True(viewModel.ChatInBubbles);
            Assert.True(settings.Current.Appearance.ChatInBubbles);
            Assert.Equal(1, settings.SaveCount);
        }

        [Test]
        public void ToggleChatInBubblesCommand_FlipsTheChoice_AndTellsTheWindow()
        {
            var settings = new FakeSettingsService();
            using var viewModel = new OverlayViewModel(settings);
            var notifications = 0;
            viewModel.PropertyChanged += (_, _) => notifications++;

            viewModel.ToggleChatInBubblesCommand.Execute(null);
            Assert.True(viewModel.ChatInBubbles);

            viewModel.ToggleChatInBubblesCommand.Execute(null);
            Assert.False(viewModel.ChatInBubbles);
            Assert.Equal(2, notifications);
        }
    }
}
