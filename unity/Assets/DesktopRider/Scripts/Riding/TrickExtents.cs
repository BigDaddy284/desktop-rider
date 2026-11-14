using System;
using UnityEngine;

namespace DesktopRider.Riding
{
    /// <summary>
    /// How much room a trick really takes, measured from its clip: how far behind and ahead of the start the bike and
    /// rider reach (front wheel included, at the very end as well), how high the helmet goes at the top of the jump,
    /// and how far out to the side. In metres, in the rider's own frame (forward, up, sideways), from where the trick starts.
    /// The window is framed from this, so nothing is cut off; a fixed guess cut off the front wheel at the end of some
    /// tricks and the helmet at the top of some jumps.
    /// </summary>
    public readonly struct TrickExtent
    {
        public TrickExtent(float back, float front, float up, float side)
        {
            Back = back; Front = front; Up = up; Side = side;
        }

        /// <summary>Metres behind the start (a positive number).</summary>
        public float Back { get; }

        /// <summary>Metres ahead of the start.</summary>
        public float Front { get; }

        /// <summary>Metres above the ground.</summary>
        public float Up { get; }

        /// <summary>Metres out to either side of the line.</summary>
        public float Side { get; }

        public override string ToString() =>
            $"back {Back:0.00} m, front {Front:0.00} m, up {Up:0.00} m, side {Side:0.00} m";
    }

    public static class TrickExtents
    {
        /// <summary>
        /// Room around each bone for what hangs off it: a wheel's radius around the axle, the helmet above the head bone.
        /// </summary>
        public const float BoneMargin = 0.32f;

        /// <summary>
        /// Plays <paramref name="clip"/> on <paramref name="model"/> at <paramref name="samples"/> moments and measures
        /// where every bone goes, in <paramref name="rider"/>'s frame, moved on by <paramref name="travel"/>(seconds) metres
        /// as the trick moves the rider. The model is left in the clip's last pose; whoever plays clips next overwrites it.
        /// </summary>
        public static TrickExtent Measure(GameObject model, AnimationClip clip, Transform rider, Func<double, double> travel, int samples = 72)
        {
            if (model == null || clip == null || rider == null) return new TrickExtent(0.6f, 0.6f, 1.6f, 0.5f);
            var bones = model.GetComponentsInChildren<Transform>(true);
            float back = 0f, front = 0f, up = 0f, side = 0f;
            double start = travel(0);
            samples = Math.Max(2, samples);
            for (int i = 0; i <= samples; i++)
            {
                float t = clip.length * i / samples;
                clip.SampleAnimation(model, t);
                float moved = (float)(travel(t) - start);
                foreach (var bone in bones)
                {
                    var p = rider.InverseTransformPoint(bone.position);
                    float z = p.z + moved;
                    back = Mathf.Max(back, -z);
                    front = Mathf.Max(front, z);
                    up = Mathf.Max(up, p.y);
                    side = Mathf.Max(side, Mathf.Abs(p.x));
                }
            }
            return new TrickExtent(back + BoneMargin, front + BoneMargin, up + BoneMargin, side + BoneMargin);
        }

        /// <summary>The eight corners of the box the trick fills, on the ground and at the top, in world space.</summary>
        public static Vector3[] Corners(TrickExtent extent, Vector3 start, float heading, float minimumSide = 0f)
        {
            var rotation = Quaternion.Euler(0f, heading, 0f);
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            float side = Mathf.Max(extent.Side, minimumSide);
            var corners = new Vector3[8];
            int k = 0;
            foreach (float along in new[] { -extent.Back, extent.Front })
                foreach (float across in new[] { -side, side })
                    foreach (float height in new[] { 0f, extent.Up })
                        corners[k++] = start + forward * along + right * across + Vector3.up * height;
            return corners;
        }
    }
}
