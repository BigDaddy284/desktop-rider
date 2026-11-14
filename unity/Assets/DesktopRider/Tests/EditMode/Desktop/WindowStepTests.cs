#nullable enable

using DesktopRider.Core.Desktop;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public sealed class WindowStepTests
    {
        [Test] public void SameSize_MovesStraightThere()
        {
            var next = WindowStep.Next(new WindowBox(10, 10, 300, 300), new WindowBox(50, 80, 300, 300), 300, 300);
            Assert.Equal(new WindowBox(50, 80, 300, 300), next);
        }

        [Test] public void Shrinking_MovesAndShrinksAtOnce()
        {
            // End of a trick: from the wide trick window to the small one over the rider.
            var next = WindowStep.Next(new WindowBox(100, 200, 1000, 500), new WindowBox(700, 300, 333, 333), 1000, 500);
            Assert.Equal(new WindowBox(700, 300, 333, 333), next);
        }

        [Test] public void Growing_GrowsWhereItIsFirst_ThenMoves()
        {
            // Start of a trick: the small window becomes the wide trick window.
            var now = new WindowBox(700, 300, 333, 333);
            var wanted = new WindowBox(100, 200, 1000, 500);
            var first = WindowStep.Next(now, wanted, 333, 333);
            Assert.Equal(new WindowBox(700, 300, 1000, 500), first);
            var second = WindowStep.Next(first, wanted, 1000, 500);       // Unity now draws at the new size
            Assert.Equal(wanted, second);
        }

        [Test] public void WiderButLower_GrowsToHoldBothFirst()
        {
            var now = new WindowBox(700, 300, 333, 333);
            var first = WindowStep.Next(now, new WindowBox(100, 350, 1000, 250), 333, 333);
            Assert.Equal(new WindowBox(700, 300, 1000, 333), first);      // keeps the old picture's height until it moves
            Assert.Equal(new WindowBox(100, 350, 1000, 250), WindowStep.Next(first, new WindowBox(100, 350, 1000, 250), 1000, 333));
        }

        [Test] public void UnityStillBehind_KeepsWaitingWhereItIs()
        {
            var grown = new WindowBox(700, 300, 1000, 500);
            Assert.Equal(grown, WindowStep.Next(grown, new WindowBox(100, 200, 1000, 500), 333, 333));
        }

        [Test] public void DrawnPart_MatchingSizes_IsTheWholeWindow()
        {
            var part = WindowStep.DrawnPart(400, 300, 400, 300);
            Assert.Equal((-200.0, 200.0, -150.0, 150.0), part);
        }

        [Test] public void DrawnPart_SmallerPicture_IsPinnedTopLeft()
        {
            var part = WindowStep.DrawnPart(1000, 500, 333, 333);
            Assert.Equal((-500.0, -167.0, -83.0, 250.0), part);
        }
    
        [Test] public void Follow_WindowTheRidersSize_CentresOnHim()
        {
            Assert.Equal((850, 400), WindowStep.Follow(0, 0, 300, 300, 1000, 550, 300, 300, 1920, 1080));
        }

        [Test] public void Follow_BigWindow_StaysPutWhileHeIsInside()
        {
            Assert.Equal((400, 200), WindowStep.Follow(400, 200, 1200, 800, 1000, 550, 300, 300, 1920, 1080));
        }

        [Test] public void Follow_BigWindow_MovesOnlyAsFarAsItMust()
        {
            // He has ridden to x 1500: his area reaches 1650, so the 1200-wide window's left edge must be at least 450.
            Assert.Equal((450, 200), WindowStep.Follow(400, 200, 1200, 800, 1500, 550, 300, 300, 1920, 1080));
        }

        [Test] public void Follow_StaysOnTheDesktop()
        {
            Assert.Equal((720, 0), WindowStep.Follow(400, 200, 1200, 800, 1900, 20, 300, 300, 1920, 1080));
        }
    }
}
