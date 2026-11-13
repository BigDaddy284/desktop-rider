using DesktopRider.Riding;
using NUnit.Framework;
using UnityEngine;

namespace DesktopRider.Tests
{
    public class DesktopViewTests
    {
        static DesktopView View() => new DesktopView(1920, 1080, 100f, 45f);

        [Test] public void Origin_IsTheDesktopCentre()
        {
            var p = View().ToDesktop(Vector3.zero);
            Assert.AreEqual(960f, p.x, 1e-3f);
            Assert.AreEqual(540f, p.y, 1e-3f);
        }

        [Test] public void OneMetreSideways_IsPixelsPerMetre()
        {
            var p = View().ToDesktop(new Vector3(1f, 0f, 0f));
            Assert.AreEqual(1060f, p.x, 1e-3f);
            Assert.AreEqual(540f, p.y, 1e-3f);
        }

        [Test] public void OneMetreAway_IsForeshortenedAndHigherUp()
        {
            var p = View().ToDesktop(new Vector3(0f, 0f, 1f));
            Assert.AreEqual(960f, p.x, 1e-3f);
            Assert.AreEqual(540f - 100f * Mathf.Sin(45f * Mathf.Deg2Rad), p.y, 1e-2f);
        }

        [Test] public void OneMetreUp_RisesOnScreen()
        {
            var p = View().ToDesktop(new Vector3(0f, 1f, 0f));
            Assert.Less(p.y, 540f);
        }

        [TestCase(0f, 0f)]
        [TestCase(1919f, 1079f)]
        [TestCase(333f, 777f)]
        [TestCase(960f, 540f)]
        public void GroundAndDesktop_RoundTrip(float x, float y)
        {
            var view = View();
            Assert.IsTrue(view.ToGround(new Vector2(x, y), out var ground));
            Assert.AreEqual(0f, ground.y, 1e-4f);
            var back = view.ToDesktop(ground);
            Assert.AreEqual(x, back.x, 1e-2f);
            Assert.AreEqual(y, back.y, 1e-2f);
        }

        [Test] public void CameraFor_TheCentre_IsTheWholeDesktopCamera()
        {
            var view = View();
            var c = view.CameraFor(view.Centre);
            Assert.AreEqual(0f, Vector3.Distance(c, view.CameraPosition), 1e-4f);
        }

        [Test] public void CameraFor_APixel_LooksAtWhatThatPixelShows()
        {
            var view = View();
            var pixel = new Vector2(1500f, 300f);
            Assert.IsTrue(view.ToGround(pixel, out var ground));
            var camera = view.CameraFor(pixel);
            var toGround = (ground - camera).normalized;
            Assert.AreEqual(1f, Vector3.Dot(toGround, view.Forward), 1e-4f);
        }

        [Test] public void WindowCorner_CentresTheWindow() =>
            Assert.AreEqual(new Vector2Int(800, 400), View().WindowCorner(new Vector2(960f, 540f), 320, 280));

        [Test] public void WindowCorner_StaysOnTheDesktop()
        {
            var view = View();
            Assert.AreEqual(new Vector2Int(0, 0), view.WindowCorner(new Vector2(-50f, -50f), 320, 280));
            Assert.AreEqual(new Vector2Int(1600, 800), view.WindowCorner(new Vector2(5000f, 5000f), 320, 280));
        }
    }
}
