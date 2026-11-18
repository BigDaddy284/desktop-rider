using System;

namespace DesktopRider.Riding
{
    /// <summary>The arithmetic of riding about: no scene, no clock, so it can be tested on its own.</summary>
    public static class RiderSteering
    {
        /// <summary>Distance between two points ignoring height.</summary>
        public static float FlatDistance(UnityEngine.Vector3 a, UnityEngine.Vector3 b)
        {
            float dx = b.x - a.x, dz = b.z - a.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Compass heading in degrees (0 = +Z, 90 = +X) from one point towards another.</summary>
        public static float HeadingTo(UnityEngine.Vector3 from, UnityEngine.Vector3 to)
        {
            return (float)(Math.Atan2(to.x - from.x, to.z - from.z) * 180.0 / Math.PI);
        }

        /// <summary>Signed shortest turn, in degrees, from one heading to another: always between -180 and 180.</summary>
        public static float ShortestTurn(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>How far to turn this step: towards the wanted heading, never more than the limit.</summary>
        public static float TurnStep(float heading, float wanted, float maxStep)
        {
            float d = ShortestTurn(heading, wanted);
            if (d > maxStep) return maxStep;
            if (d < -maxStep) return -maxStep;
            return d;
        }

        /// <summary>The speed to aim for: cruise when far away, slowing so the bike can stop at the arrival distance.</summary>
        public static float SpeedFor(float distance, float cruise, float braking, float arriveDistance)
        {
            float room = distance - arriveDistance;
            if (room <= 0f) return 0f;
            float stoppable = (float)Math.Sqrt(2.0 * braking * room);
            return stoppable < cruise ? stoppable : cruise;
        }

        /// <summary>
        /// The fastest the bike may go and still turn onto a place <paramref name="distance"/> metres away that is
        /// <paramref name="turnAhead"/> degrees off its heading. Turning slows with speed as in <see cref="FreeRider"/>
        /// (<paramref name="maxTurnRate"/> times speed / cruise + 0.25, at most the full rate). Without this a wide-turning
        /// outfit (the sidecar: 6.3 m/s, 80 degrees a second) circles a place close beside it for ever (Boss, 17 November 2026).
        /// </summary>
        public static float SpeedForTurn(float distance, float turnAhead, float maxTurnRate, float cruise)
        {
            float angle = Math.Min(90f, Math.Abs(turnAhead));
            if (angle < 1f || cruise <= 0f || maxTurnRate <= 0f) return cruise;
            double radius = 0.9 * distance / (2.0 * Math.Sin(angle * Math.PI / 180.0));   // the arc onto the place, with a margin
            double omega = maxTurnRate * Math.PI / 180.0;
            double full = omega * radius;                       // the speed that turn allows at the full turning rate
            double limit = full >= 0.75 * cruise ? Math.Min(cruise, full) : 0.25 * full / (1.0 - full / cruise);
            return (float)Math.Max(0.15, Math.Min(cruise, limit));
        }

        /// <summary>The radius of the bike's tightest turn at <paramref name="speed"/>, turning <paramref name="maxTurnRate"/> degrees a second.</summary>
        public static float TurnRadius(float speed, float maxTurnRate) =>
            maxTurnRate <= 0f ? 0f : speed / (maxTurnRate * (float)(Math.PI / 180.0));

        /// <summary>
        /// How long the straight run-up to a trick is (see <see cref="DesktopRider.Tricks.RunUpPath"/>): at least a turning
        /// radius, so the bike has settled on the trick's line, and for a trick that begins moving, room to get up to its speed.
        /// </summary>
        public static float RunUpLength(float turnRadius, float entrySpeed, float acceleration)
        {
            float lineUp = turnRadius;
            float speedUp = acceleration > 0f && entrySpeed > 0f ? entrySpeed * entrySpeed / (2f * acceleration) + 0.5f * turnRadius : 0f;
            return Math.Max(1.2f, Math.Max(lineUp, speedUp));
        }

        /// <summary>
        /// The speed to aim for <paramref name="toStart"/> metres before a trick's start so the bike is going
        /// <paramref name="entrySpeed"/> as it gets there (0: stops there), no faster than <paramref name="topSpeed"/>.
        /// </summary>
        public static float SpeedToEnter(float toStart, float entrySpeed, float braking, float topSpeed)
        {
            if (toStart <= 0f || braking <= 0f) return Math.Min(entrySpeed, topSpeed);
            return Math.Min(topSpeed, (float)Math.Sqrt(entrySpeed * entrySpeed + 2.0 * braking * toStart));
        }

        /// <summary>Lean angle in degrees for a turn rate (degrees a second) at a speed (metres a second), limited to maxLean.</summary>
        public static float LeanFor(float turnRateDegrees, float speed, float maxLean)
        {
            const double g = 9.81;
            double lateral = speed * (turnRateDegrees * Math.PI / 180.0);   // v * omega
            double lean = Math.Atan2(lateral, g) * 180.0 / Math.PI * 3.0;    // exaggerated threefold for a cartoon read
            if (lean > maxLean) lean = maxLean;
            if (lean < -maxLean) lean = -maxLean;
            return (float)lean;
        }

        /// <summary>
        /// How much of the left and right corner poses to show for a lean (degrees, positive leaning right, as
        /// <see cref="LeanFor"/> gives): none when upright, all of one side at full lean, eased in so a small wobble
        /// does not throw a leg out.
        /// </summary>
        public static (float Left, float Right) CornerWeights(float lean, float maxLean)
        {
            if (maxLean <= 0f) return (0f, 0f);
            float amount = Math.Min(1f, Math.Abs(lean) / maxLean);
            amount = amount < 0.12f ? 0f : (amount - 0.12f) / 0.6f;            // ignore small wobbles, full pose by 72%
            amount = Math.Min(1f, amount);
            amount = amount * amount * (3f - 2f * amount);
            return lean > 0f ? (0f, amount) : (amount, 0f);
        }
    }

    /// <summary>
    /// When the rider puts the inside leg out, and for how long. A real rider gets the leg out as the corner is lined
    /// up, before the bike is leaned over, and keeps it out until the bike is upright again. So the pose follows the
    /// turn still to come as well as the lean, comes in over about a third of a second, goes back more slowly, and
    /// stays out a moment once it is out, so a short turn does not flick the leg out and straight back.
    /// </summary>
    public sealed class CornerBlend
    {
        /// <summary>Degrees of turn still to come that count as a full corner.</summary>
        public float FullTurn = 50f;

        /// <summary>Turns smaller than this (degrees still to come) do not bring the leg out.</summary>
        public float SmallTurn = 12f;

        /// <summary>Seconds for the leg to come all the way out, and to go all the way back.</summary>
        public float InSeconds = 0.3f, OutSeconds = 0.6f;

        /// <summary>Once out, the leg stays out at least this long.</summary>
        public float HoldSeconds = 0.5f;

        float amount, held;
        int side;                       // +1 right, -1 left, 0 none

        public float Left => side < 0 ? amount : 0f;
        public float Right => side > 0 ? amount : 0f;

        /// <summary>Puts the leg straight back on the peg, at once (a trick takes over the whole body).</summary>
        public void Reset()
        {
            amount = 0f;
            held = 0f;
            side = 0;
        }

        /// <summary>
        /// <paramref name="turnAhead"/>: degrees still to turn to face where the bike is going (positive to the right).
        /// <paramref name="lean"/>: the bike's lean now (positive to the right). Returns the left and right weights.
        /// </summary>
        public (float Left, float Right) Update(float turnAhead, float lean, float maxLean, float seconds, bool riding)
        {
            float ahead = Math.Abs(turnAhead) <= SmallTurn ? 0f : Math.Min(1f, (Math.Abs(turnAhead) - SmallTurn) / Math.Max(1f, FullTurn - SmallTurn));
            var leaning = RiderSteering.CornerWeights(lean, maxLean);
            float leanAmount = Math.Max(leaning.Left, leaning.Right);
            int wantSide = ahead > leanAmount ? Math.Sign(turnAhead) : (leaning.Right > 0f ? 1 : leaning.Left > 0f ? -1 : 0);
            float want = riding ? Math.Max(ahead, leanAmount) : 0f;
            if (want <= 0f) wantSide = 0;

            held = Math.Max(0f, held - seconds);
            if (side != 0 && wantSide != side && amount > 0f)
            {
                // Turning the other way (or straightening up): bring this leg back first, unless it has only just come out.
                if (held > 0f && riding && wantSide == 0) return (Left, Right);
                amount = Math.Max(0f, amount - seconds / OutSeconds);
                if (amount <= 0f) side = 0;
                return (Left, Right);
            }
            if (side == 0 && wantSide != 0) side = wantSide;
            if (want > amount)
            {
                amount = Math.Min(want, amount + seconds / InSeconds);
                if (amount > 0.5f) held = HoldSeconds;
            }
            else if (held <= 0f || !riding)
            {
                amount = Math.Max(want, amount - seconds / OutSeconds);
            }
            if (amount <= 0f) side = 0;
            return (Left, Right);
        }
    }
}
