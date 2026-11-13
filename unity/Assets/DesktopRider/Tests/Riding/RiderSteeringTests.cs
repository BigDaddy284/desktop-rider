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
    }
}
