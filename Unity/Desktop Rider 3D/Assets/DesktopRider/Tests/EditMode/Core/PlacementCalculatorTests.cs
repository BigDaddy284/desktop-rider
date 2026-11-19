#nullable enable

using System;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Placement;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class PlacementCalculatorTests
    {
        private static readonly PixelSize Size = new(200, 150);

        // A 1920x1080 main screen with a 40px taskbar, and a second 1280x1024 screen to its left.
        private static readonly ScreenArea Main = new(@"\\.\DISPLAY1", new PixelRect(0, 0, 1920, 1040), IsPrimary: true);
        private static readonly ScreenArea Left = new(@"\\.\DISPLAY2", new PixelRect(-1280, 0, 1280, 1024), IsPrimary: false);
        private static readonly ScreenArea[] Both = new[] { Left, Main };

        [Test]
        public void DefaultPosition_IsBottomRightOfTheMainScreen_AboveTheTaskbar()
        {
            var position = PlacementCalculator.DefaultPosition(Size, Both);

            Assert.Equal(new PixelPoint(1920 - 200 - 48, 1040 - 150 - 48), position);
        }

        [Test]
        public void DefaultPosition_UsesTheFirstScreen_WhenNoneIsMarkedAsMain()
        {
            var position = PlacementCalculator.DefaultPosition(Size, new[] { Left });

            Assert.Equal(new PixelPoint(-200 - 48, 1024 - 150 - 48), position);
        }

        [Test]
        public void DefaultPosition_WithNoScreens_IsTheOrigin()
        {
            Assert.Equal(new PixelPoint(0, 0), PlacementCalculator.DefaultPosition(Size, Array.Empty<ScreenArea>()));
        }

        [Test]
        public void DefaultPosition_OnAScreenSmallerThanTheCharacter_KeepsTheTopLeftVisible()
        {
            var tiny = new ScreenArea("tiny", new PixelRect(100, 100, 120, 90), IsPrimary: true);

            Assert.Equal(new PixelPoint(100, 100), PlacementCalculator.DefaultPosition(Size, new[] { tiny }));
        }

        [Test]
        public void Restore_WithNothingSaved_UsesTheDefault()
        {
            Assert.Equal(PlacementCalculator.DefaultPosition(Size, Both), PlacementCalculator.Restore(null, Size, Both));
        }

        [Test]
        public void Restore_ReturnsTheSavedPosition_WhenItIsFullyOnScreen()
        {
            var saved = new SavedPosition { X = 300, Y = 400, Monitor = Main.DeviceName };

            Assert.Equal(new PixelPoint(300, 400), PlacementCalculator.Restore(saved, Size, Both));
        }

        [Test]
        public void Restore_WorksOnAScreenWithNegativeCoordinates()
        {
            var saved = new SavedPosition { X = -900, Y = 200, Monitor = Left.DeviceName };

            Assert.Equal(new PixelPoint(-900, 200), PlacementCalculator.Restore(saved, Size, Both));
        }

        [Test]
        public void Restore_PullsAPartlyHiddenPositionBackOntoItsRememberedScreen()
        {
            // Straddling the two screens, mostly on Main, but remembered as belonging to Left.
            var saved = new SavedPosition { X = -50, Y = 2000, Monitor = Left.DeviceName };
            var straddling = new SavedPosition { X = -50, Y = 500, Monitor = Left.DeviceName };

            Assert.Equal(PlacementCalculator.DefaultPosition(Size, Both), PlacementCalculator.Restore(saved, Size, Both));
            Assert.Equal(new PixelPoint(-200, 500), PlacementCalculator.Restore(straddling, Size, Both));
        }

        [Test]
        public void Restore_WhenTheMonitorWasUnplugged_FallsBackToTheDefault()
        {
            var saved = new SavedPosition { X = -900, Y = 200, Monitor = Left.DeviceName };

            var position = PlacementCalculator.Restore(saved, Size, new[] { Main });

            Assert.Equal(PlacementCalculator.DefaultPosition(Size, new[] { Main }), position);
        }

        [Test]
        public void Restore_AfterAResolutionChange_KeepsTheCharacterInside()
        {
            // Still partly visible on the smaller screen, so it is pulled fully inside, not sent to the default spot.
            var saved = new SavedPosition { X = 1200, Y = 600, Monitor = Main.DeviceName };
            var smaller = new ScreenArea(Main.DeviceName, new PixelRect(0, 0, 1280, 680), IsPrimary: true);

            Assert.Equal(new PixelPoint(1080, 530), PlacementCalculator.Restore(saved, Size, new[] { smaller }));
        }

        [Test]
        public void Restore_AfterAResolutionChange_UsesTheDefault_WhenTheOldSpotNoLongerExists()
        {
            var saved = new SavedPosition { X = 1700, Y = 880, Monitor = Main.DeviceName };
            var smaller = new ScreenArea(Main.DeviceName, new PixelRect(0, 0, 1280, 680), IsPrimary: true);

            Assert.Equal(new PixelPoint(1032, 482), PlacementCalculator.Restore(saved, Size, new[] { smaller }));
        }

        [Test]
        public void Restore_WithAnUnknownMonitorName_StillUsesAPositionThatIsOnScreen()
        {
            var saved = new SavedPosition { X = 300, Y = 400, Monitor = "renamed" };

            Assert.Equal(new PixelPoint(300, 400), PlacementCalculator.Restore(saved, Size, Both));
        }
        [TestCase(double.NaN, 0)]
        [TestCase(0, double.PositiveInfinity)]
        public void Restore_IgnoresUnusableNumbers(double x, double y)
        {
            var saved = new SavedPosition { X = x, Y = y };

            Assert.Equal(PlacementCalculator.DefaultPosition(Size, Both), PlacementCalculator.Restore(saved, Size, Both));
        }

        [Test]
        public void Restore_CopesWithAbsurdlyLargeNumbers()
        {
            var saved = new SavedPosition { X = 1e300, Y = -1e300 };

            Assert.Equal(PlacementCalculator.DefaultPosition(Size, Both), PlacementCalculator.Restore(saved, Size, Both));
        }
        [TestCase(-100, 500, 0, 500)] // off the left edge of Main only when Left is absent
        [TestCase(1850, 500, 1720, 500)] // off the right edge
        [TestCase(500, -80, 500, 0)] // off the top
        [TestCase(500, 1000, 500, 890)] // into the taskbar
        [TestCase(500, 500, 500, 500)] // already fine
        public void KeepOnScreen_NudgesBackInside_OnASingleScreen(int x, int y, int expectedX, int expectedY)
        {
            var position = PlacementCalculator.KeepOnScreen(new PixelPoint(x, y), Size, new[] { Main });

            Assert.Equal(new PixelPoint(expectedX, expectedY), position);
        }

        [Test]
        public void KeepOnScreen_ChoosesTheScreenTheCharacterIsMostlyOn()
        {
            // 150px of the 200px width is on Left, so it snaps fully onto Left.
            Assert.Equal(new PixelPoint(-200, 300), PlacementCalculator.KeepOnScreen(new PixelPoint(-150, 300), Size, Both));

            // 150px is on Main, so it snaps fully onto Main.
            Assert.Equal(new PixelPoint(0, 300), PlacementCalculator.KeepOnScreen(new PixelPoint(-50, 300), Size, Both));
        }

        [Test]
        public void KeepOnScreen_WhenCompletelyOffEveryScreen_MovesToTheNearestOne()
        {
            var farRight = PlacementCalculator.KeepOnScreen(new PixelPoint(5000, 300), Size, Both);
            var farLeft = PlacementCalculator.KeepOnScreen(new PixelPoint(-5000, 300), Size, Both);

            Assert.Equal(new PixelPoint(1720, 300), farRight);
            Assert.Equal(new PixelPoint(-1280, 300), farLeft);
        }

        [Test]
        public void KeepOnScreen_WithNoScreens_LeavesThePositionAlone()
        {
            Assert.Equal(new PixelPoint(7, 9), PlacementCalculator.KeepOnScreen(new PixelPoint(7, 9), Size, Array.Empty<ScreenArea>()));
            Assert.Null(PlacementCalculator.ScreenFor(new PixelPoint(7, 9), Size, Array.Empty<ScreenArea>()));
        }

        [Test]
        public void ScreenFor_ReportsTheScreenUnderTheCharacter()
        {
            Assert.Same(Left, PlacementCalculator.ScreenFor(new PixelPoint(-600, 100), Size, Both));
            Assert.Same(Main, PlacementCalculator.ScreenFor(new PixelPoint(600, 100), Size, Both));
        }

        [Test]
        public void OverlapArea_IsZeroForRectanglesThatOnlyTouch()
        {
            var a = new PixelRect(0, 0, 100, 100);

            Assert.Equal(0, a.OverlapArea(new PixelRect(100, 0, 100, 100)));
            Assert.Equal(2500, a.OverlapArea(new PixelRect(50, 50, 100, 100)));
            Assert.Equal(new PixelPoint(0, 0), a.Location);
            Assert.Equal(new PixelSize(100, 100), a.Size);
        }

        // The rider at 60% size is roughly 240 by 200 pixels; the conversation window is taller than that.
        private static readonly PixelSize Bubble = new(300, 80);
        private static readonly PixelSize Chat = new(392, 492);

        [Test]
        public void Beside_PrefersAboveTheCharacter_CentredOnIt()
        {
            var rider = new PixelRect(800, 600, 240, 200);

            var placement = PlacementCalculator.Beside(rider, Bubble, Both);

            Assert.Equal(CompanionSide.Above, placement.Side);
            Assert.Equal(new PixelPoint(800 + ((240 - 300) / 2), 600 - 80), placement.Position);
        }

        [Test]
        public void Beside_InTheDefaultCorner_StaysOnTheScreen()
        {
            var rider = PixelRect.From(PlacementCalculator.DefaultPosition(new PixelSize(240, 200), Both), new PixelSize(240, 200));

            var placement = PlacementCalculator.Beside(rider, Chat, Both);

            Assert.Equal(CompanionSide.Above, placement.Side);
            Assert.Equal(rider.Y - Chat.Height, placement.Position.Y);
            Assert.Equal(1920 - Chat.Width, placement.Position.X);
        }

        [Test]
        public void Beside_AppliesTheGap_AndANegativeGapTucksIn()
        {
            var rider = new PixelRect(800, 600, 240, 200);

            Assert.Equal(600 - 80 - 10, PlacementCalculator.Beside(rider, Bubble, Both, gap: 10).Position.Y);
            Assert.Equal(600 - 80 + 12, PlacementCalculator.Beside(rider, Bubble, Both, gap: -12).Position.Y);
        }

        [Test]
        public void Beside_WithNoRoomAbove_GoesToTheLeft()
        {
            var rider = new PixelRect(900, 20, 240, 200);

            var placement = PlacementCalculator.Beside(rider, Chat, Both);

            Assert.Equal(CompanionSide.Left, placement.Side);
            Assert.Equal(900 - Chat.Width, placement.Position.X);
            Assert.Equal(0, placement.Position.Y);
        }

        [Test]
        public void Beside_WithNoRoomAboveOrLeft_GoesToTheRight()
        {
            var rider = new PixelRect(10, 20, 240, 200);

            var placement = PlacementCalculator.Beside(rider, Chat, new[] { Main });

            Assert.Equal(CompanionSide.Right, placement.Side);
            Assert.Equal(new PixelPoint(250, 0), placement.Position);
        }

        [Test]
        public void Beside_WithRoomOnlyBelow_GoesBelow()
        {
            var narrow = new ScreenArea("narrow", new PixelRect(0, 0, 500, 1000), IsPrimary: true);
            var rider = new PixelRect(130, 20, 240, 200);

            var placement = PlacementCalculator.Beside(rider, Chat, new[] { narrow });

            Assert.Equal(CompanionSide.Below, placement.Side);
            Assert.Equal(new PixelPoint(130 + ((240 - 392) / 2), 220), placement.Position);
        }

        [Test]
        public void Beside_WithNoRoomAnywhere_StaysFullyOnTheScreen()
        {
            var small = new ScreenArea("small", new PixelRect(0, 0, 500, 520), IsPrimary: true);
            var rider = new PixelRect(130, 160, 240, 200);

            var placement = PlacementCalculator.Beside(rider, Chat, new[] { small });

            Assert.Equal(CompanionSide.Above, placement.Side);
            Assert.Equal(new PixelPoint(54, 0), placement.Position);
        }

        [Test]
        public void Beside_UsesTheScreenTheCharacterIsOn()
        {
            var rider = new PixelRect(-300, 700, 240, 200);

            var placement = PlacementCalculator.Beside(rider, Chat, Both);

            Assert.Equal(CompanionSide.Above, placement.Side);
            Assert.Equal(-Chat.Width, placement.Position.X);
            Assert.True(placement.Position.X >= Left.WorkingArea.X);
        }

        [Test]
        public void Beside_WithNoScreens_StillAnswers()
        {
            var placement = PlacementCalculator.Beside(new PixelRect(100, 100, 240, 200), Bubble, Array.Empty<ScreenArea>());

            Assert.Equal(new PixelPoint(70, 20), placement.Position);
        }

        // The typing bubble for "Chat in bubbles" asks to go below the rider.
        private static readonly PixelSize TypingBubble = new(340, 54);

        [Test]
        public void Beside_WithAPreferredSide_UsesItWhenThereIsRoom()
        {
            var rider = new PixelRect(800, 400, 240, 200);

            var placement = PlacementCalculator.Beside(rider, TypingBubble, Both, gap: -12, preferred: CompanionSide.Below);

            Assert.Equal(CompanionSide.Below, placement.Side);
            Assert.Equal(new PixelPoint(800 + ((240 - 340) / 2), 600 - 12), placement.Position);
        }

        [Test]
        public void Beside_InTheDefaultCorner_TheTypingBubbleJustFitsBelowTheRider()
        {
            var rider = PixelRect.From(PlacementCalculator.DefaultPosition(new PixelSize(240, 200), Both), new PixelSize(240, 200));

            var placement = PlacementCalculator.Beside(rider, TypingBubble, Both, gap: -12, preferred: CompanionSide.Below);

            Assert.Equal(CompanionSide.Below, placement.Side);
            Assert.True(placement.Position.Y + TypingBubble.Height <= Main.WorkingArea.Bottom);
            Assert.True(placement.Position.X + TypingBubble.Width <= Main.WorkingArea.Right);
        }

        [Test]
        public void Beside_WhenThePreferredSideHasNoRoom_TriesTheOthersInTurn()
        {
            var rider = new PixelRect(800, 1040 - 200, 240, 200);

            var placement = PlacementCalculator.Beside(rider, TypingBubble, Both, preferred: CompanionSide.Below);

            Assert.Equal(CompanionSide.Above, placement.Side);
        }

        [Test]
        public void Beside_WithNoRoomAnywhere_KeepsToThePreferredSide_OnTheScreen()
        {
            var small = new ScreenArea("small", new PixelRect(0, 0, 400, 260), IsPrimary: true);
            var rider = new PixelRect(80, 30, 240, 200);

            var placement = PlacementCalculator.Beside(rider, new PixelSize(340, 54), new[] { small }, preferred: CompanionSide.Below);

            Assert.Equal(CompanionSide.Below, placement.Side);
            Assert.Equal(new PixelPoint(30, 206), placement.Position);
        }
    }
}
