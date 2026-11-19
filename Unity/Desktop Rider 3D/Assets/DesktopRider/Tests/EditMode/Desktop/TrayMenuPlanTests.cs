#nullable enable

using DesktopRider.Core.Desktop;
using DesktopRider.Core.ViewModels;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class TrayMenuPlanTests
    {
        [Test]
        public void EachCommand_GetsItsOwnNumber_AndRunsWhenChosen()
        {
            int hides = 0, exits = 0;
            var plan = new TrayMenuPlan(new[]
            {
                new TrayMenuEntry("Hide the rider", new RelayCommand(() => hides++)),
                TrayMenuEntry.Separator,
                new TrayMenuEntry("Exit Desktop Rider", new RelayCommand(() => exits++)),
            });

            Assert.Equal(3, plan.Lines.Count);
            Assert.Equal(TrayMenuPlan.FirstId, plan.Lines[0].Id);
            Assert.True(plan.Lines[1].IsSeparator);
            Assert.NotEqual(plan.Lines[0].Id, plan.Lines[2].Id);

            Assert.True(plan.Run(plan.Lines[2].Id));
            Assert.Equal(0, hides);
            Assert.Equal(1, exits);
        }

        [Test]
        public void ClosingTheMenuWithoutAChoice_RunsNothing()
        {
            int runs = 0;
            var plan = new TrayMenuPlan(new[] { new TrayMenuEntry("Show the rider", new RelayCommand(() => runs++)) });

            Assert.False(plan.Run(0));
            Assert.False(plan.Run(99));
            Assert.Equal(0, runs);
        }

        [Test]
        public void Ticks_AreKept()
        {
            var plan = new TrayMenuPlan(new[] { new TrayMenuEntry("Always on top", new RelayCommand(() => { }), IsChecked: true) });

            Assert.True(plan.Lines[0].IsChecked);
            Assert.Equal("Always on top", plan.Lines[0].Text);
        }

        [Test]
        public void SubMenus_AreNumberedToo()
        {
            string chosen = string.Empty;
            var plan = new TrayMenuPlan(new[]
            {
                new TrayMenuEntry("Model", null, Children: new[]
                {
                    new TrayMenuEntry("small", new RelayCommand(() => chosen = "small"), IsChecked: true),
                    new TrayMenuEntry("large", new RelayCommand(() => chosen = "large")),
                }),
                new TrayMenuEntry("Exit", new RelayCommand(() => chosen = "exit")),
            });

            var children = plan.Lines[0].Children;
            Assert.NotNull(children);
            Assert.Equal(2, children!.Count);
            Assert.False(plan.Lines[0].IsSeparator);
            Assert.True(children[0].IsChecked);

            Assert.True(plan.Run(children[1].Id));
            Assert.Equal("large", chosen);
            Assert.True(plan.Run(plan.Lines[1].Id));
            Assert.Equal("exit", chosen);
        }
    }
}
