#nullable enable

using System;
using System.Linq;
using System.Numerics;
using DesktopRider.Tricks;
using NUnit.Framework;

namespace DesktopRider.Tricks.Tests
{
    public class RunUpPathTests
    {
        static float HeadingOf(Vector2 from, Vector2 to) => (float)(Math.Atan2(to.X - from.X, to.Y - from.Y) * 180.0 / Math.PI);

        static float Turn(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        [Test]
        public void FromAnywhere_TheShapeIsSmoothAndEndsOnTheTricksLine()
        {
            var random = new Random(11);
            for (int k = 0; k < 500; k++)
            {
                var from = new Vector2((float)random.NextDouble() * 40f - 20f, (float)random.NextDouble() * 40f - 20f);
                var start = new Vector2((float)random.NextDouble() * 40f - 20f, (float)random.NextDouble() * 40f - 20f);
                float heading = (float)random.NextDouble() * 360f, trickHeading = (float)random.NextDouble() * 360f;
                const float radius = 5.4f, runUp = 4.5f;
                var path = RunUpPath.Plan(from, heading, start, trickHeading, radius, runUp, beyond: 10f);
                var p = path.Points;

                Assert.True(Vector2.Distance(p[0], from) < 1e-4f, "case " + k + ": starts where the rider is");
                Assert.True(Math.Abs(Turn(heading, HeadingOf(p[0], p[2]))) < 6f, "case " + k + ": sets off the way he faces");
                for (int i = 1; i < p.Count; i++)
                    Assert.True(Vector2.Distance(p[i - 1], p[i]) <= 0.2f + 1e-3f, "case " + k + ": no gaps");
                // No corner sharper than the turning circle allows: the heading changes at most step / radius between points.
                for (int i = 2; i < p.Count; i++)
                {
                    float bend = Math.Abs(Turn(HeadingOf(p[i - 2], p[i - 1]), HeadingOf(p[i - 1], p[i])));
                    float allowed = (float)((Vector2.Distance(p[i - 1], p[i]) + Vector2.Distance(p[i - 2], p[i - 1])) / radius * 180.0 / Math.PI);
                    Assert.True(bend <= allowed + 3f, "case " + k + ": a corner of " + bend + " degrees at point " + i);
                }
                // The trick's start is on the path, StartDistance along it, reached along the trick's heading.
                int atStart = path.Advance(start, 0, p.Count);
                Assert.True(Vector2.Distance(p[atStart], start) < 0.11f, "case " + k + ": goes through the start");
                Assert.Equal(0.0, path.ToStart(atStart), 1);
                Assert.True(Math.Abs(Turn(trickHeading, HeadingOf(p[atStart - 5], p[atStart]))) < 1f, "case " + k + ": arrives on the trick's line");
                Assert.True(Vector2.Distance(p[p.Count - 1], start + TrickRun.Direction(trickHeading) * 10f) < 1e-3f, "case " + k + ": carries on past the start");
            }
        }

        [Test]
        public void AlreadyOnTheLine_ItIsJustTheStraight()
        {
            var path = RunUpPath.Plan(new Vector2(0f, -10f), 0f, Vector2.Zero, 0f, 5f, 4f, beyond: 2f);
            Assert.All(path.Points, q => Assert.True(Math.Abs(q.X) < 1e-3f, "stays on the line"));
            Assert.Equal(10.0, path.StartDistance, 3);
            Assert.Equal(12.0, path.Length, 3);
        }

        [Test]
        public void Aim_IsAheadAlongThePath_AndToStartCountsDown()
        {
            var path = RunUpPath.Plan(new Vector2(0f, -10f), 0f, Vector2.Zero, 0f, 5f, 4f, beyond: 2f);
            int i = path.Advance(new Vector2(0.3f, -6f), 0);
            Assert.Equal(-6.0, path.Points[i].Y, 3);
            Assert.Equal(6.0, path.ToStart(i), 3);
            Assert.Equal(-4.0, path.Aim(i, 2f).Y, 3);
            Assert.Equal(2.0, path.Aim(i, 100f).Y, 3);    // never past the end
        }

        [Test]
        public void Advance_OnlyMovesOn()
        {
            var path = RunUpPath.Plan(new Vector2(0f, -10f), 0f, Vector2.Zero, 0f, 5f, 4f, beyond: 2f);
            int i = path.Advance(new Vector2(0f, -5f), 0);
            Assert.Equal(i, path.Advance(new Vector2(0f, -9f), i));
        }
    }
}
