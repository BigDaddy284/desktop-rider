#nullable enable

using System.Threading.Tasks;
using DesktopRider.Core.Desktop;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class FreeRidingMenuTests
    {
        [Test]
        public void IsOffByDefault_AndShowsUnticked()
        {
            var menu = new FreeRidingMenu(new FakeSettingsService());

            var line = Assert.Single(menu.BuildEntries());
            Assert.Equal(FreeRidingMenu.MenuText, line.Text);
            Assert.False(line.IsChecked);
            Assert.False(menu.Enabled);
        }

        [Test]
        public void ChoosingTheLine_TurnsItOn_ThenOff()
        {
            var settings = new FakeSettingsService();
            var menu = new FreeRidingMenu(settings);

            Assert.Single(menu.BuildEntries()).Command!.Execute(null);
            Assert.True(settings.Current.Behaviour.FreeRiding);
            Assert.True(Assert.Single(menu.BuildEntries()).IsChecked);

            Assert.Single(menu.BuildEntries()).Command!.Execute(null);
            Assert.False(settings.Current.Behaviour.FreeRiding);
        }

        [Test]
        public async Task WhenSavingFails_TheChoiceStillAppliesForNow()
        {
            var settings = new FakeSettingsService { FailSaves = true };
            var menu = new FreeRidingMenu(settings);

            await menu.SetAsync(true);

            Assert.True(menu.Enabled);
        }
    }
}
