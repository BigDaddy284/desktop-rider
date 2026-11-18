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
    
        [Test] public void SpeedForTurn_StraightAhead_IsCruise() =>
            Assert.AreEqual(6.3f, RiderSteering.SpeedForTurn(3f, 0.5f, 80f, 6.3f), 1e-4f);

        [Test] public void SpeedForTurn_FarAway_IsCruise() =>
            Assert.AreEqual(6.3f, RiderSteering.SpeedForTurn(40f, 60f, 80f, 6.3f), 1e-4f);

        [Test] public void SpeedForTurn_APlaceCloseBeside_SlowsTheSidecarEnoughToGetThere()
        {
            // 2 m away, 90 degrees off: the arc onto it has a radius of about 1 m. At cruise the sidecar turns on 4.5 m.
            float limit = RiderSteering.SpeedForTurn(2f, 90f, 80f, 6.3f);
            Assert.Less(limit, 6.3f);
            float rate = 80f * Mathf.Min(1f, limit / 6.3f + 0.25f) * Mathf.Deg2Rad;
            Assert.LessOrEqual(limit / rate, 1f + 1e-3f, "turning circle must fit the arc onto the place");
            Assert.GreaterOrEqual(limit, 0.15f);
        }

        [Test] public void SpeedForTurn_HarveyCruisingToANearPlace_IsBarelyChanged() =>
            Assert.AreEqual(2.6f, RiderSteering.SpeedForTurn(6f, 30f, 110f, 2.6f), 1e-4f);

        [Test] public void TurnRadius_SidecarAtCruise_IsAbout4_5Metres() =>
            Assert.AreEqual(4.51f, RiderSteering.TurnRadius(6.3f, 80f), 0.01f);

        [Test] public void RunUpLength_IsThreeTurnRadii_OrEnoughToGetUpToSpeed()
        {
            Assert.AreEqual(1.2f, RiderSteering.RunUpLength(0.2f, 0f, 2.5f), 1e-4f);            // never shorter than 1.2 m
            Assert.AreEqual(3f * 1.35f, RiderSteering.RunUpLength(1.35f, 0f, 2.5f), 1e-4f);     // Harvey: lining up decides
            Assert.AreEqual(13f * 13f / 12f + 4.5f, RiderSteering.RunUpLength(4.5f, 13f, 6f), 1e-3f);   // the sidecar's Double: speed decides
        }

        [Test] public void LineUpAim_IsOnTheLine_AheadOfTheBike()
        {
            var aim = RiderSteering.LineUpAim(new Vector3(2f, 0f, -5f), Vector3.zero, Vector3.forward, 1.5f);
            Assert.AreEqual(0f, aim.x, 1e-4f);
            Assert.AreEqual(-3.5f, aim.z, 1e-4f);
            Assert.AreEqual(-5f, RiderSteering.Along(new Vector3(2f, 0f, -5f), Vector3.zero, Vector3.forward), 1e-4f);
        }

        [Test] public void SpeedToEnter_ReachesTheEntrySpeedAtTheStart()
        {
            Assert.AreEqual(6.3f, RiderSteering.SpeedToEnter(0f, 6.3f, 9f, 13f), 1e-4f);
            Assert.AreEqual(0f, RiderSteering.SpeedToEnter(-1f, 0f, 4f, 2.6f), 1e-4f);
            Assert.AreEqual(Mathf.Sqrt(2f * 4f * 0.5f), RiderSteering.SpeedToEnter(0.5f, 0f, 4f, 2.6f), 1e-4f);
            Assert.AreEqual(13f, RiderSteering.SpeedToEnter(50f, 13f, 9f, 13f), 1e-4f);
        }

        // Rides the run-up to a trick the way FreeRider does, with the same steering pieces, 1/30 s a step.
        static (float Seconds, float HeadingError, float Speed, float Miss)? RideRunUp(float cruise, float accel, float braking, float turnRate,
            float entry, Vector3 from, float heading, Vector3 start, float trickHeading)
        {
            const float dt = 1f / 30f, arrive = 0.6f;
            var forward = Quaternion.Euler(0f, trickHeading, 0f) * Vector3.forward;
            float radius = RiderSteering.TurnRadius(cruise, turnRate);
            float runUp = RiderSteering.RunUpLength(radius, entry, accel);
            var target = start - forward * runUp;
            bool firstLeg = true;
            Vector3 at = from;
            float speed = 0f;
            for (int i = 0; i < 30 * 60; i++)
            {
                float distance = RiderSteering.FlatDistance(at, target);
                if (firstLeg && distance < Mathf.Max(arrive + 0.4f, radius)) firstLeg = false;
                float toStart = 0f, top = cruise, topTurn = turnRate, wanted;
                if (firstLeg) wanted = RiderSteering.SpeedToEnter(distance + runUp, entry, braking, cruise);
                else
                {
                    toStart = -RiderSteering.Along(at, start, forward);
                    target = RiderSteering.LineUpAim(at, start, forward, Mathf.Max(0.6f, 0.7f * radius));
                    distance = RiderSteering.FlatDistance(at, target);
                    top = Mathf.Max(cruise, entry);
                    topTurn = turnRate * top / cruise;
                    wanted = RiderSteering.SpeedToEnter(toStart, entry, braking, top);
                }
                float off = RiderSteering.ShortestTurn(heading, RiderSteering.HeadingTo(at, target));
                if (wanted > 0f) wanted = Mathf.Min(wanted, RiderSteering.SpeedForTurn(distance, off, topTurn, top));
                speed = Mathf.MoveTowards(speed, wanted, (wanted > speed ? accel : braking) * dt);
                if (speed > 0.05f)
                {
                    float rate = turnRate * Mathf.Clamp01(speed / cruise + 0.25f);
                    heading += RiderSteering.TurnStep(heading, RiderSteering.HeadingTo(at, target), rate * dt);
                    at += Quaternion.Euler(0f, heading, 0f) * Vector3.forward * (speed * dt);
                }
                if (!firstLeg && toStart <= 0.05f && speed <= entry + 0.15f)
                    return (i * dt, Mathf.Abs(RiderSteering.ShortestTurn(heading, trickHeading)), speed, RiderSteering.FlatDistance(at, start));
            }
            return null;
        }

        [TestCase(2.6f, 2.5f, 4f, 110f, 0f, TestName = "RunUp_Harvey_LinesUpAndStops")]
        [TestCase(6.3f, 6f, 9f, 80f, 6.3f, TestName = "RunUp_Sidecar_GroundTrick_LinesUpAtSpeed")]
        [TestCase(6.3f, 6f, 9f, 80f, 13f, TestName = "RunUp_Sidecar_Double_LinesUpAtSpeed")]
        public void RunUp_FromAnywhere_ArrivesFacingTheTrick(float cruise, float accel, float braking, float turnRate, float entry)
        {
            // The sidecar circled slowly near the start, then was snapped round to face the jump (Boss, 17 November 2026).
            var random = new System.Random(7);
            float[] turns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
            for (int k = 0; k < 120; k++)
            {
                float heading = (float)random.NextDouble() * 360f;
                bool inPlace = k % 2 == 0;
                var start = inPlace ? Vector3.zero : new Vector3((float)random.NextDouble() * 16f - 8f, 0f, (float)random.NextDouble() * 16f - 8f);
                float trickHeading = inPlace ? heading + turns[k / 2 % turns.Length] : (float)random.NextDouble() * 360f;
                var result = RideRunUp(cruise, accel, braking, turnRate, entry, Vector3.zero, heading, start, trickHeading);
                Assert.IsTrue(result.HasValue, "case " + k + ": never got to the start");
                var r = result.Value;
                Assert.Less(r.Seconds, 15f, "case " + k + ": took too long");
                Assert.Less(r.HeadingError, 15f, "case " + k + ": arrived facing the wrong way");
                Assert.Less(r.Miss, 1.5f, "case " + k + ": arrived off the line");
                if (entry > 1f) Assert.Greater(r.Speed, 0.7f * Mathf.Min(entry, cruise), "case " + k + ": should arrive moving");
            }
        }
    }
}
