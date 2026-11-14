#nullable enable

using System;
using System.Numerics;
using DesktopRider.Core.Desktop;
using DesktopRider.Core.Placement;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class RidingAreaTests
    {
        // A 1920x1080 main screen with a 40px taskbar, and a 1280x1024 screen to its left, as Windows reports them.
        private static readonly ScreenArea Main = new(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1040), IsPrimary: true);
        private static readonly ScreenArea Left = new(@"\\.\DISPLAY2", new PixelRect(-1280, 0, 1280, 1024), IsPrimary: false);

        private static RidingArea TwoScreens(out PixelPoint offset) => RidingArea.FromWindows(new[] { Left, Main }, out offset);

        [Test]
        public void FromWindows_MovesEverythingToStartAtZero_AndPutsTheMainScreenFirst()
        {
            var area = TwoScreens(out var offset);

            Assert.Equal(new PixelPoint(1280, 0), offset);
            Assert.Equal(new PixelRect(1280, 0, 1920, 1040), area.Main);
            Assert.Equal(new PixelRect(0, 0, 1280, 1024), area.Screens[1]);
            Assert.Equal(new PixelSize(3200, 1040), area.Size);
        }

        [Test]
        public void FromWindows_HandlesAScreenAbove()
        {
            var above = new ScreenArea("top", new PixelRect(0, -1080, 1920, 1080), false);

            var area = RidingArea.FromWindows(new[] { Main, above }, out var offset);

            Assert.Equal(new PixelPoint(0, 1080), offset);
            Assert.Equal(new PixelRect(0, 1080, 1920, 1040), area.Main);
            Assert.Equal(new PixelSize(1920, 2120), area.Size);
        }

        [Test]
        public void FromWindows_UsesTheFirstScreen_WhenNoneIsMarkedAsMain()
        {
            var a = new ScreenArea("a", new PixelRect(0, 0, 800, 600), false);
            var b = new ScreenArea("b", new PixelRect(800, 0, 800, 600), false);

            Assert.Equal(new PixelRect(0, 0, 800, 600), RidingArea.FromWindows(new[] { a, b }, out _).Main);
        }

        [Test]
        public void NoScreens_IsRefused()
        {
            Assert.Throws<ArgumentException>(() => new RidingArea(Array.Empty<PixelRect>()));
            Assert.Throws<ArgumentException>(() => RidingArea.FromWindows(Array.Empty<ScreenArea>(), out _));
        }

        [Test]
        public void ScreenAt_FindsTheScreenUnderAPoint()
        {
            var area = TwoScreens(out _);

            Assert.Equal(0, area.ScreenAt(new Vector2(2000, 500)));
            Assert.Equal(1, area.ScreenAt(new Vector2(100, 500)));
        }

        [Test]
        public void KeepOnScreen_LeavesAPointOnAScreenAlone()
        {
            var area = TwoScreens(out _);

            Assert.Equal(new Vector2(2000, 500), area.KeepOnScreen(new Vector2(2000, 500)));
        }

        [Test]
        public void KeepOnScreen_PullsAPointInTheGapBackToTheNearestScreen()
        {
            var area = TwoScreens(out _);

            // Below the left screen's bottom (1024) but above the main screen's bottom would not exist; use 1035 near the left screen.
            var kept = area.KeepOnScreen(new Vector2(600, 1035));

            Assert.Equal(new Vector2(600, 1024), kept);
        }

        [Test]
        public void KeepOnScreen_PullsAPointOffTheDesktopBackOn()
        {
            var area = RidingArea.Single(1920, 1080);

            Assert.Equal(new Vector2(0, 1080), area.KeepOnScreen(new Vector2(-50, 2000)));
        }

        [Test]
        public void PickTarget_StaysInsideTheMargin_OnASingleScreen()
        {
            var area = RidingArea.Single(1000, 800);
            var random = new Random(4);

            for (int i = 0; i < 200; i++)
            {
                var t = area.PickTarget(new Vector2(500, 400), random, 0.1f);
                Assert.InRange(t.X, 100f, 900f);
                Assert.InRange(t.Y, 80f, 720f);
            }
        }

        [Test]
        public void PickTarget_NeverChangesScreen_WhenTheChanceIsZero()
        {
            var area = TwoScreens(out _);
            var random = new Random(7);

            for (int i = 0; i < 200; i++)
            {
                Assert.Equal(0, area.ScreenAt(area.PickTarget(new Vector2(2000, 500), random, 0.1f, otherScreenChance: 0)));
            }
        }

        [Test]
        public void PickTarget_AlwaysChangesScreen_WhenTheChanceIsOne()
        {
            var area = TwoScreens(out _);
            var random = new Random(9);

            for (int i = 0; i < 200; i++)
            {
                Assert.Equal(1, area.ScreenAt(area.PickTarget(new Vector2(2000, 500), random, 0.1f, otherScreenChance: 1)));
            }
        }

        [Test]
        public void PickTarget_VisitsEveryOtherScreen_WithThreeScreens()
        {
            var area = new RidingArea(new[] { new PixelRect(0, 0, 100, 100), new PixelRect(100, 0, 100, 100), new PixelRect(200, 0, 100, 100) });
            var random = new Random(11);
            var seen = new bool[3];

            for (int i = 0; i < 300; i++)
            {
                seen[area.ScreenAt(area.PickTarget(new Vector2(150, 50), random, 0.1f, otherScreenChance: 1))] = true;
            }

            Assert.True(seen[0] && seen[2]);
            Assert.False(seen[1]);
        }

        [Test]
        public void WindowCorner_CentresTheWindow()
        {
            var area = RidingArea.Single(1920, 1080);

            Assert.Equal(new PixelPoint(780, 360), area.WindowCorner(new Vector2(960, 540), new PixelSize(360, 360)));
        }

        [Test]
        public void WindowCorner_KeepsTheWindowInsideTheWholeArea()
        {
            var area = TwoScreens(out _);

            Assert.Equal(new PixelPoint(0, 0), area.WindowCorner(new Vector2(10, 10), new PixelSize(360, 360)));
            Assert.Equal(new PixelPoint(3200 - 360, 1040 - 360), area.WindowCorner(new Vector2(5000, 5000), new PixelSize(360, 360)));
        }

        [Test]
        public void MainCentre_IsTheMiddleOfTheMainScreen()
        {
            var area = TwoScreens(out _);

            Assert.Equal(new Vector2(1280 + 960, 520), area.MainCentre);
        }

        [Test]
        public void IsOnScreen_KnowsTheGapsAndKeepsAMargin()
        {
            var area = new RidingArea(new[] { new PixelRect(0, 0, 1000, 800), new PixelRect(1000, 200, 800, 600) });

            Assert.True(area.IsOnScreen(new Vector2(500, 400)));
            Assert.True(area.IsOnScreen(new Vector2(1400, 500)));
            Assert.False(area.IsOnScreen(new Vector2(1400, 100)));      // above the shorter right-hand screen
            Assert.False(area.IsOnScreen(new Vector2(-1, 400)));
            Assert.True(area.IsOnScreen(new Vector2(10, 400)));
            Assert.False(area.IsOnScreen(new Vector2(10, 400), margin: 20));
        }
    }
}
