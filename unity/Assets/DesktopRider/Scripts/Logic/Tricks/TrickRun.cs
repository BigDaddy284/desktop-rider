#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DesktopRider.Tricks
{
    /// <summary>A prop put down on the desktop ground for one trick.</summary>
    public readonly struct PlacedProp
    {
        public PlacedProp(string name, Vector2 position, float heading)
        {
            Name = name; Position = position; Heading = heading;
        }

        public string Name { get; }

        /// <summary>The start of the piece, on the ground, in the middle of the track. X is east, Y is north (Unity's z).</summary>
        public Vector2 Position { get; }

        /// <summary>Degrees clockwise from north, the same way the rider faces.</summary>
        public float Heading { get; }
    }

    /// <summary>
    /// One trick being ridden on the desktop: where its props go, where the bike is at each moment and when it is done.
    /// Positions are on the ground in metres: X across, Y away from the viewer (Unity's x and z). Headings are degrees
    /// turned clockwise seen from above, 0 being straight up the screen, like a Unity object's yaw.
    /// </summary>
    public sealed class TrickRun
    {
        private double _seconds;

        public TrickRun(TrickAnimation animation, Vector2 start, float heading)
        {
            Animation = animation ?? throw new ArgumentNullException(nameof(animation));
            Start = start;
            Heading = heading;
            Forward = Direction(heading);
            Props = animation.Props.Select(p => new PlacedProp(p.Prop, start + Forward * (float)p.X, heading)).ToArray();
        }

        public TrickAnimation Animation { get; }
        public Vector2 Start { get; }
        public float Heading { get; }
        public Vector2 Forward { get; }
        public IReadOnlyList<PlacedProp> Props { get; }

        /// <summary>How fast the trick plays; 1 is normal (the animation-speed setting).</summary>
        public double Speed { get; set; } = 1.0;

        /// <summary>Seconds into the clip.</summary>
        public double ClipTime => Math.Min(_seconds, Animation.Duration.TotalSeconds);

        public bool IsFinished => _seconds >= Animation.Duration.TotalSeconds;

        /// <summary>From 0 at the start to 1 when done.</summary>
        public double Progress => Animation.Duration.TotalSeconds <= 0 ? 1.0 : ClipTime / Animation.Duration.TotalSeconds;

        /// <summary>Where the bike is now.</summary>
        public Vector2 Position => Start + Forward * (float)(Animation.TravelAt(ClipTime) - Animation.TravelAt(0));

        /// <summary>Where the bike will be when the trick is over.</summary>
        public Vector2 End => Start + Forward * (float)(Animation.Distance - Animation.TravelAt(0));

        public void Advance(TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero) return;
            _seconds += elapsed.TotalSeconds * Math.Max(0.0, Speed);
        }

        /// <summary>The ground direction for a heading: 0 is +Y, 90 is +X.</summary>
        public static Vector2 Direction(float heading)
        {
            double a = heading * Math.PI / 180.0;
            return new Vector2((float)Math.Sin(a), (float)Math.Cos(a));
        }

        /// <summary>The headings tried, in order, when the way the rider faces has no room: small turns first, then back the other way.</summary>
        public static readonly float[] HeadingTurns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };

        /// <summary>
        /// Picks a heading close to <paramref name="current"/> in which the whole trick, props and all, stays where the
        /// rider can be seen. <paramref name="fits"/> says whether a ground point is on a screen. Null if nowhere fits.
        /// </summary>
        /// <param name="halfWidth">Half the width of the props, so their sides stay on screen too.</param>
        public static float? PickHeading(TrickAnimation animation, Vector2 start, float current, Func<Vector2, bool> fits, float halfWidth = 0.6f, float step = 0.25f)
        {
            if (animation == null) throw new ArgumentNullException(nameof(animation));
            if (fits == null) throw new ArgumentNullException(nameof(fits));
            var (from, to) = animation.Reach;
            foreach (float turn in HeadingTurns)
            {
                float heading = Normalise(current + turn);
                var forward = Direction(heading);
                var side = new Vector2(forward.Y, -forward.X) * halfWidth;
                bool ok = true;
                for (double d = from; ok; d = Math.Min(to, d + step))
                {
                    var p = start + forward * (float)d;
                    ok = fits(p) && fits(p + side) && fits(p - side);
                    if (d >= to) break;
                }
                if (ok) return heading;
            }
            return null;
        }

        public static float Normalise(float heading)
        {
            heading %= 360f;
            return heading < 0 ? heading + 360f : heading;
        }
    }

    /// <summary>Chooses the next trick: never the same one twice in a row, and only ones the model and layout both have.</summary>
    public sealed class TrickPicker
    {
        private readonly Random _random;
        private string? _last;

        public TrickPicker(Random? random = null) => _random = random ?? new Random();

        public string? Last => _last;

        /// <param name="available">Tricks that can be ridden.</param>
        /// <param name="groundOnly">True when there is only room for a trick that stays on the ground.</param>
        public string? Next(IEnumerable<TrickAnimation> available, bool groundOnly = false)
        {
            var choices = available.Where(a => !groundOnly || a.Stage == "ground").Select(a => a.Name).Distinct().ToList();
            if (choices.Count > 1 && _last != null) choices.Remove(_last);
            if (choices.Count == 0) return null;
            _last = choices[_random.Next(choices.Count)];
            return _last;
        }
    }
}
