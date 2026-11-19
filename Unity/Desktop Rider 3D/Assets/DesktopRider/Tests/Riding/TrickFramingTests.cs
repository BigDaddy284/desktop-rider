using System;
using DesktopRider.Riding;
using DesktopRider.Tricks;
using NUnit.Framework;
using UnityEngine;

namespace DesktopRider.Tests
{
    /// <summary>
    /// The window during a trick must hold the whole bike and rider at every moment: the front wheel at the end and the
    /// helmet at the top of the jump were cut off when the room was a fixed guess.
    /// </summary>
    public class TrickFramingTests
    {
        static DesktopView View() => new DesktopView(1920, 1080, 100f, 45f);

        static TrickAnimation Trick(double distance, params TrickPropPlacement[] props) =>
            new TrickAnimation("Test", "air", 4, 4.0, props, new[] { 0.0, distance * 0.25, distance * 0.5, distance * 0.75, distance });

        [Test] public void Corners_SpanTheExtentAlongTheHeading()
        {
            var corners = TrickExtents.Corners(new TrickExtent(1f, 3f, 2f, 0.5f), Vector3.zero, 0f);
            Assert.AreEqual(8, corners.Length);
            float minZ = float.MaxValue, maxZ = float.MinValue, maxY = 0f, maxX = 0f;
            foreach (var c in corners) { minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z); maxY = Mathf.Max(maxY, c.y); maxX = Mathf.Max(maxX, c.x); }
            Assert.AreEqual(-1f, minZ, 1e-4f);
            Assert.AreEqual(3f, maxZ, 1e-4f);
            Assert.AreEqual(2f, maxY, 1e-4f);
            Assert.AreEqual(0.5f, maxX, 1e-4f);
        }

        [Test] public void Corners_TurnWithTheHeading()
        {
            var corners = TrickExtents.Corners(new TrickExtent(0f, 3f, 1f, 0.2f), Vector3.zero, 90f);
            float maxX = float.MinValue;
            foreach (var c in corners) maxX = Mathf.Max(maxX, c.x);
            Assert.AreEqual(3f, maxX, 1e-4f, "facing +X the front reaches 3 m along X");
        }

        [Test] public void Corners_UseTheWiderOfTheBikeAndTheProps()
        {
            var corners = TrickExtents.Corners(new TrickExtent(0f, 1f, 1f, 0.3f), Vector3.zero, 0f, 0.8f);
            float maxX = 0f;
            foreach (var c in corners) maxX = Mathf.Max(maxX, c.x);
            Assert.AreEqual(0.8f, maxX, 1e-4f);
        }

        [TestCase(0f)] [TestCase(30f)] [TestCase(-120f)] [TestCase(180f)]
        public void FrameFor_HoldsTheHighestAndFurthestPointOfTheTrick(float heading)
        {
            var view = View();
            var extent = new TrickExtent(0.8f, 6.5f, 2.4f, 0.5f);
            var start = new Vector3(1f, 0f, -2f);
            var frame = TrickRunner.FrameFor(view, extent, Trick(5.0), start, heading, 0.55f);
            foreach (var corner in TrickExtents.Corners(extent, start, heading, 0.55f))
                Assert.IsTrue(frame.Contains(view.ToDesktop(corner)), "corner " + corner + " is outside the frame " + frame);
        }

        [Test] public void FrameFor_HoldsAPropThatReachesFurtherThanTheBike()
        {
            var view = View();
            var trick = Trick(4.0, new TrickPropPlacement("DR_Prop_JumpLanding", 3.0, 4.0));    // ends 7 m out
            var frame = TrickRunner.FrameFor(view, new TrickExtent(0.5f, 4.5f, 1.5f, 0.5f), trick, Vector3.zero, 0f, 0.55f);
            Assert.IsTrue(frame.Contains(view.ToDesktop(new Vector3(0.55f, 0f, 7f))));
        }

        [Test] public void TrickWindow_IsTheShapeOfTheTrick_NotASquare()
        {
            var window = FreeRider.TrickWindow(new Rect(500.2f, 100.6f, 300f, 800f), 1920, 1080);
            Assert.AreEqual(new RectInt(500, 100, 301, 801), window);
        }

        [Test] public void TrickWindow_IsNeverBiggerThanTheDesktop()
        {
            // A long jump straight up the screen is taller than a 1080-pixel screen; Windows would cut such a window short.
            var window = FreeRider.TrickWindow(new Rect(800f, -150f, 320f, 1400f), 1920, 1080);
            Assert.AreEqual(0, window.y);
            Assert.AreEqual(1080, window.height);
            Assert.AreEqual(800, window.x);
            Assert.AreEqual(320, window.width);
        }

        [Test] public void Measure_FindsTheTopOfTheJumpAndTheNoseAtTheEnd()
        {
            var rider = new GameObject("Rider");
            try
            {
                var model = new GameObject("Model");
                model.transform.SetParent(rider.transform, false);
                var head = new GameObject("head");
                head.transform.SetParent(model.transform, false);
                var nose = new GameObject("nose");
                nose.transform.SetParent(model.transform, false);
                nose.transform.localPosition = new Vector3(0f, 0.3f, 0.7f);

                var clip = new AnimationClip { legacy = true };
                // The head rises from 1.2 m to 2.0 m half way through and comes back down.
                clip.SetCurve("head", typeof(Transform), "localPosition.y", new AnimationCurve(new Keyframe(0f, 1.2f), new Keyframe(0.5f, 2.0f), new Keyframe(1f, 1.2f)));
                clip.SetCurve("nose", typeof(Transform), "localPosition.z", AnimationCurve.Constant(0f, 1f, 0.7f));
                clip.SetCurve("nose", typeof(Transform), "localPosition.y", AnimationCurve.Constant(0f, 1f, 0.3f));

                // The trick moves the rider 4 m forward over its one second.
                var extent = TrickExtents.Measure(model, clip, rider.transform, t => 4.0 * t, 40);
                Assert.AreEqual(2.0f + TrickExtents.BoneMargin, extent.Up, 0.02f, "helmet room at the top");
                Assert.AreEqual(4.7f + TrickExtents.BoneMargin, extent.Front, 0.02f, "front wheel room at the end");
                Assert.AreEqual(TrickExtents.BoneMargin, extent.Back, 0.02f);
                UnityEngine.Object.DestroyImmediate(clip);
            }
            finally { UnityEngine.Object.DestroyImmediate(rider); }
        }
    }
}
