#nullable enable

using DesktopRider.Core.Desktop;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class TrickMenuTests
    {
        [Test]
        public void ChoosingTheLine_AsksForATrick()
        {
            var menu = new TrickMenu(() => true);
            int asked = 0;
            menu.Requested += (s, e) => asked++;

            var line = Assert.Single(menu.BuildEntries());
            Assert.Equal(TrickMenu.MenuText, line.Text);
            Assert.False(line.IsChecked);
            line.Command!.Execute(null);

            Assert.Equal(1, asked);
        }

        [Test]
        public void IsLeftOut_WhileTricksAreNotAvailable()
        {
            bool available = false;
            var menu = new TrickMenu(() => available);
            Assert.Empty(menu.BuildEntries());

            available = true;
            Assert.Single(menu.BuildEntries());
        }
    }
}
