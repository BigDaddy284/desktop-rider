#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;

namespace DesktopRider.Tricks
{
    /// <summary>
    /// The way the rider rides to a trick: from where he is, facing the way he is, round onto the trick's line a run-up's
    /// length before its start, then straight along the line to the start and on past it. The turns are arcs of one radius,
    /// drawn wide enough to ride at speed (the shortest of the four turn-straight-turn paths). Before this the rider aimed
    /// at points and had to slow to a crawl to turn tight enough (Boss, 17 and 18 November 2026).
    /// Ground points: X is east, Y is north; headings in degrees, 0 north, 90 east.
    /// </summary>
    public sealed class RunUpPath
    {
        private readonly List<Vector2> _points = new List<Vector2>();
        private readonly List<float> _along = new List<float>();

        private RunUpPath() { }

        /// <summary>The path, a point every <c>step</c> metres or so.</summary>
        public IReadOnlyList<Vector2> Points => _points;

        /// <summary>How far along the path the trick's start is, in metres.</summary>
        public float StartDistance { get; private set; }

        /// <summary>The length of the path, run-up and the stretch past the start included.</summary>
        public float Length => _along[_along.Count - 1];

        /// <summary>
        /// Plans the path from <paramref name="from"/>, facing <paramref name="heading"/>, to the trick's
        /// <paramref name="start"/> facing <paramref name="trickHeading"/>, with turns of <paramref name="radius"/> metres,
        /// the last <paramref name="runUp"/> metres straight along the trick's line, and <paramref name="beyond"/> metres more past the start.
        /// </summary>
        public static RunUpPath Plan(Vector2 from, float heading, Vector2 start, float trickHeading, float radius, float runUp,
            float beyond = 30f, float step = 0.2f)
        {
            if (!(radius > 0)) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!(step > 0)) throw new ArgumentOutOfRangeException(nameof(step));
            var path = new RunUpPath();
            var forward = TrickRun.Direction(trickHeading);
            var lineStart = start - forward * Math.Max(0f, runUp);
            path.Add(from);
            path.AddTurns(from, heading, lineStart, trickHeading, radius, step);
            path.AddLine(lineStart, step);
            path.AddLine(start, step);
            path.StartDistance = path.Length;
            path.AddLine(start + forward * Math.Max(step, beyond), step);
            return path;
        }

        /// <summary>
        /// Where the rider is along the path: the nearest point to <paramref name="at"/> among the next few after
        /// <paramref name="index"/> (the rider only moves on along it).
        /// </summary>
        public int Advance(Vector2 at, int index, int window = 40)
        {
            index = Math.Max(0, Math.Min(index, _points.Count - 1));
            int best = index;
            float bestDistance = float.MaxValue;
            for (int j = index; j < Math.Min(_points.Count, index + window); j++)
            {
                float d = Vector2.DistanceSquared(_points[j], at);
                if (d < bestDistance) { bestDistance = d; best = j; }
            }
            return best;
        }

        /// <summary>Metres still to ride from point <paramref name="index"/> to the trick's start (negative past it).</summary>
        public float ToStart(int index) => StartDistance - _along[Math.Max(0, Math.Min(index, _along.Count - 1))];

        /// <summary>The point to steer for: <paramref name="lookAhead"/> metres on along the path from point <paramref name="index"/>.</summary>
        public Vector2 Aim(int index, float lookAhead)
        {
            index = Math.Max(0, Math.Min(index, _points.Count - 1));
            int k = index;
            while (k < _points.Count - 1 && _along[k] - _along[index] < lookAhead) k++;
            return _points[k];
        }

        private void Add(Vector2 p)
        {
            if (_points.Count == 0) { _points.Add(p); _along.Add(0f); return; }
            float d = Vector2.Distance(_points[_points.Count - 1], p);
            if (d < 1e-5f) return;
            _points.Add(p);
            _along.Add(_along[_along.Count - 1] + d);
        }

        // Turn, straight, turn (Dubins): the shortest of left-straight-left, right-straight-right, left-straight-right and
        // right-straight-left. Worked in the usual maths angles (anticlockwise from east) to keep the formulas standard.
        private void AddTurns(Vector2 from, float heading, Vector2 to, float toHeading, float r, float step)
        {
            double a0 = (90.0 - heading) * Math.PI / 180.0, a1 = (90.0 - toHeading) * Math.PI / 180.0;
            double bestLength = double.MaxValue;
            int bestS0 = 1, bestS1 = 1;
            double bestPhi = a0, bestArc0 = 0, bestArc1 = 0;
            Vector2 bestC0 = from, bestC1 = to;
            foreach (var (s0, s1) in new[] { (1, 1), (-1, -1), (1, -1), (-1, 1) })
            {
                var c0 = Centre(from, a0, s0, r);
                var c1 = Centre(to, a1, s1, r);
                double dx = c1.X - c0.X, dy = c1.Y - c0.Y, d = Math.Sqrt(dx * dx + dy * dy);
                double phi, straight;
                if (s0 == s1)
                {
                    phi = d < 1e-9 ? a0 : Math.Atan2(dy, dx);
                    straight = d;
                }
                else
                {
                    if (d < 2 * r) continue;
                    phi = Math.Atan2(dy, dx) + s0 * Math.Asin(2 * r / d);
                    straight = Math.Sqrt(Math.Max(0, d * d - 4 * r * r));
                }
                double arc0 = Wrap(s0 * (phi - a0)), arc1 = Wrap(s1 * (a1 - phi));
                double length = r * arc0 + straight + r * arc1;
                if (length < bestLength)
                {
                    bestLength = length; bestS0 = s0; bestS1 = s1; bestPhi = phi; bestArc0 = arc0; bestArc1 = arc1; bestC0 = c0; bestC1 = c1;
                }
            }
            AddArc(bestC0, bestS0, a0, bestArc0, r, step);
            AddLine(OnCircle(bestC1, bestS1, bestPhi, r), step);       // the straight, to where the last turn begins
            AddArc(bestC1, bestS1, bestPhi, bestArc1, r, step);
        }

        private void AddLine(Vector2 to, float step)
        {
            var from = _points[_points.Count - 1];
            int n = Math.Max(1, (int)Math.Ceiling(Vector2.Distance(from, to) / step));
            for (int i = 1; i <= n; i++) Add(Vector2.Lerp(from, to, (float)i / n));
        }

        private static Vector2 OnCircle(Vector2 c, int s, double a, float r) =>
            new Vector2((float)(c.X + s * r * Math.Sin(a)), (float)(c.Y - s * r * Math.Cos(a)));

        // The centre of the turning circle of a bike at p facing maths angle a, turning left (s = 1) or right (s = -1).
        private static Vector2 Centre(Vector2 p, double a, int s, float r) =>
            new Vector2((float)(p.X - s * r * Math.Sin(a)), (float)(p.Y + s * r * Math.Cos(a)));

        private void AddArc(Vector2 c, int s, double start, double sweep, float r, float step)
        {
            int n = Math.Max(1, (int)Math.Ceiling(r * sweep / step));
            for (int i = 0; i <= n; i++)
            {
                Add(OnCircle(c, s, start + s * sweep * i / n, r));
            }
        }

        private static double Wrap(double a)
        {
            const double turn = 2 * Math.PI;
            a %= turn;
            return a < 0 ? a + turn : a;
        }
    }
}
