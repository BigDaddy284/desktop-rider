using DesktopRider.Riding;
using NUnit.Framework;
using UnityEngine;

namespace DesktopRider.Tests
{
    public class RiderSteeringTests
    {
        [Test] public void FlatDistance_IgnoresHeight() =>
            Assert.AreEqual(5f, RiderSteering.FlatDistance(new Vector3(0, 0, 0), new Vector3(3, 99, 4)), 1e-4f);

        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, 90f)]
        [TestCase(0f, -1f, 180f)]
        [TestCase(-1f, 0f, -90f)]
        public void HeadingTo_UsesCompassDegrees(float x, float z, float expected) =>
            Assert.AreEqual(expected, RiderSteering.HeadingTo(Vector3.zero, new Vector3(x, 0, z)), 1e-3f);

        [TestCase(350f, 10f, 20f)]
        [TestCase(10f, 350f, -20f)]
        [TestCase(0f, 180f, 180f)]
        [TestCase(720f, 90f, 90f)]
        [TestCase(-170f, 170f, -20f)]
        public void ShortestTurn_GoesTheShortWay(float from, float to, float expected) =>
            Assert.AreEqual(expected, RiderSteering.ShortestTurn(from, to), 1e-3f);

        [Test] public void TurnStep_IsLimited()
        {
            Assert.AreEqual(5f, RiderSteering.TurnStep(0f, 90f, 5f), 1e-4f);
            Assert.AreEqual(-5f, RiderSteering.TurnStep(0f, -90f, 5f), 1e-4f);
            Assert.AreEqual(3f, RiderSteering.TurnStep(0f, 3f, 5f), 1e-4f);
        }

        [Test] public void SpeedFor_StopsAtArrival()
        {
            Assert.AreEqual(0f, RiderSteering.SpeedFor(0.5f, 3f, 4f, 0.6f));
            Assert.AreEqual(0f, RiderSteering.SpeedFor(0.6f, 3f, 4f, 0.6f));
        }

        [Test] public void SpeedFor_CruisesWhenFar() =>
            Assert.AreEqual(3f, RiderSteering.SpeedFor(50f, 3f, 4f, 0.6f));

        [Test] public void SpeedFor_SlowsEnoughToStopInTheRoomLeft()
        {
            float v = RiderSteering.SpeedFor(1.1f, 3f, 4f, 0.6f);          // half a metre of room
            Assert.AreEqual(2f, v, 1e-4f);                                  // v^2 = 2 * 4 * 0.5
            Assert.Less(v, 3f);
        }

        [Test] public void LeanFor_IsZeroWhenStraightOrStopped()
        {
            Assert.AreEqual(0f, RiderSteering.LeanFor(0f, 3f, 28f), 1e-4f);
            Assert.AreEqual(0f, RiderSteering.LeanFor(90f, 0f, 28f), 1e-4f);
        }

        [Test] public void LeanFor_FollowsTheTurnAndIsLimited()
        {
            Assert.Greater(RiderSteering.LeanFor(60f, 2.6f, 28f), 0f);
            Assert.Less(RiderSteering.LeanFor(-60f, 2.6f, 28f), 0f);
            Assert.AreEqual(28f, RiderSteering.LeanFor(720f, 5f, 28f), 1e-4f);
            Assert.AreEqual(-28f, RiderSteering.LeanFor(-720f, 5f, 28f), 1e-4f);
        }

        [Test] public void CornerWeights_NoneWhenUpright_OrForASmallWobble()
        {
            Assert.AreEqual((0f, 0f), RiderSteering.CornerWeights(0f, 38f));
            Assert.AreEqual((0f, 0f), RiderSteering.CornerWeights(3f, 38f));
            Assert.AreEqual((0f, 0f), RiderSteering.CornerWeights(-3f, 38f));
        }

        [Test] public void CornerWeights_LeaningRightShowsTheRightCorner_AndLeftTheLeft()
        {
            var right = RiderSteering.CornerWeights(20f, 38f);
            var left = RiderSteering.CornerWeights(-20f, 38f);
            Assert.Greater(right.Right, 0f); Assert.AreEqual(0f, right.Left);
            Assert.Greater(left.Left, 0f); Assert.AreEqual(0f, left.Right);
            Assert.AreEqual(right.Right, left.Left, 1e-5f);
        }

        [Test] public void CornerWeights_GrowWithTheLean_AndReachTheFullPose()
        {
            float last = 0f;
            for (float lean = 0f; lean <= 38f; lean += 2f)
            {
                float w = RiderSteering.CornerWeights(lean, 38f).Right;
                Assert.GreaterOrEqual(w, last);
                Assert.LessOrEqual(w, 1f);
                last = w;
            }
            Assert.AreEqual(1f, RiderSteering.CornerWeights(38f, 38f).Right, 1e-5f);
            Assert.AreEqual(1f, RiderSteering.CornerWeights(-30f, 38f).Left, 1e-5f);
        }

        static (float Left, float Right) Run(CornerBlend blend, float turnAhead, float lean, float seconds, bool riding = true)
        {
            (float, float) w = (0f, 0f);
            for (float t = 0f; t < seconds - 1e-4f; t += 0.02f) w = blend.Update(turnAhead, lean, 38f, 0.02f, riding);
            return w;
        }

        [Test] public void CornerBlend_LegComesOutForATurnAhead_BeforeTheBikeLeans()
        {
            var blend = new CornerBlend();
            var w = Run(blend, 70f, 0f, 0.4f);          // a big turn to the right is coming; the bike is still upright
            Assert.AreEqual(1f, w.Right, 1e-4f);
            Assert.AreEqual(0f, w.Left);
        }

        [Test] public void CornerBlend_ComesInSmoothly_NotAllAtOnce()
        {
            var blend = new CornerBlend();
            var w = blend.Update(-70f, 0f, 38f, 0.02f, true);
            Assert.Greater(w.Left, 0f);
            Assert.Less(w.Left, 0.2f);
        }

        [Test] public void CornerBlend_StaysOutAMoment_ThenGoesBackGently()
        {
            var blend = new CornerBlend();
            Run(blend, 70f, 0f, 0.4f);
            var held = Run(blend, 0f, 0f, 0.3f);         // the turn is done, but the leg has only just come out
            Assert.AreEqual(1f, held.Right, 1e-4f);
            var going = Run(blend, 0f, 0f, 0.4f);
            Assert.Greater(going.Right, 0f);              // still on its way back, not snapped in
            Assert.Less(going.Right, 1f);
            Assert.AreEqual(0f, Run(blend, 0f, 0f, 1f).Right, 1e-4f);
        }

        [Test] public void CornerBlend_Reset_PutsTheLegBackAtOnce()
        {
            var blend = new CornerBlend();
            Run(blend, 70f, 20f, 0.4f);
            blend.Reset();
            Assert.AreEqual(0f, blend.Left);
            Assert.AreEqual(0f, blend.Right);
            var w = blend.Update(0f, 0f, 38f, 0.02f, true);  // no hold left over to keep it out
            Assert.AreEqual((0f, 0f), w);
        }

        [Test] public void CornerBlend_SmallTurnsLeaveTheLegOnThePeg()
        {
            var blend = new CornerBlend();
            var w = Run(blend, 8f, 2f, 1f);
            Assert.AreEqual((0f, 0f), w);
        }

        [Test] public void CornerBlend_ChangingDirection_PutsOneLegBackBeforeTheOtherComesOut()
        {
            var blend = new CornerBlend();
            Run(blend, 70f, 0f, 1f);
            for (int i = 0; i < 200; i++)
            {
                var w = blend.Update(-70f, 0f, 38f, 0.02f, true);
                Assert.False(w.Left > 0f && w.Right > 0f, "both legs out at once");
            }
            Assert.AreEqual(1f, blend.Left, 1e-4f);
        }

        [Test] public void CornerBlend_NotRiding_PutsTheLegBack()
        {
            var blend = new CornerBlend();
            Run(blend, 70f, 0f, 1f);
            var w = Run(blend, 70f, 0f, 1f, riding: false);
            Assert.AreEqual((0f, 0f), w);
        }
    }
}
