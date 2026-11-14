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
    }
}
