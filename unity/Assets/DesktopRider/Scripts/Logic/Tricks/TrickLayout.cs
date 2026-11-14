#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace DesktopRider.Tricks
{
    /// <summary>One ramp, landing or whoop section, as exported from Blender.</summary>
    public sealed class TrickProp
    {
        public TrickProp(string name, string kind, double length, double width, double height)
        {
            Name = name; Kind = kind; Length = length; Width = width; Height = height;
        }

        public string Name { get; }

        /// <summary>"dirt" or "steel".</summary>
        public string Kind { get; }

        public double Length { get; }
        public double Width { get; }
        public double Height { get; }
    }

    /// <summary>Where a prop stands for one trick: <see cref="X"/> metres ahead of where the bike starts.</summary>
    public sealed class TrickPropPlacement
    {
        public TrickPropPlacement(string prop, double x, double length)
        {
            Prop = prop; X = x; Length = length;
        }

        public string Prop { get; }
        public double X { get; }
        public double Length { get; }
    }

    /// <summary>
    /// One trick: its clip (both rigs play a take of the same name), the props it needs and how far the bike has moved
    /// forward at each frame. The bones stay in place; the whole rider is moved forward by <see cref="TravelAt"/>.
    /// </summary>
    public sealed class TrickAnimation
    {
        private readonly double[] _travel;

        public TrickAnimation(string name, string stage, int frames, double framesPerSecond, IReadOnlyList<TrickPropPlacement> props, double[] travel)
        {
            if (frames < 1) throw new ArgumentOutOfRangeException(nameof(frames), "A trick needs at least one frame.");
            if (framesPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            if (travel.Length < 2) throw new ArgumentException("A trick needs a travel distance for each frame.", nameof(travel));
            Name = name; Stage = stage; Frames = frames; FramesPerSecond = framesPerSecond; Props = props; _travel = travel;
        }

        /// <summary>Also the name of the take in the tricks model.</summary>
        public string Name { get; }

        /// <summary>"air" for jumps and freestyle tricks, "ground" for whoops and stoppies.</summary>
        public string Stage { get; }

        public int Frames { get; }
        public double FramesPerSecond { get; }
        public IReadOnlyList<TrickPropPlacement> Props { get; }

        /// <summary>How long the trick takes at normal speed.</summary>
        public TimeSpan Duration => TimeSpan.FromTicks((long)Math.Round(Seconds * TimeSpan.TicksPerSecond));

        /// <summary>The same as <see cref="Duration"/> but exact (Unity's TimeSpan.FromSeconds rounds to milliseconds).</summary>
        public double Seconds => Frames / FramesPerSecond;

        /// <summary>How far the bike has gone when the trick ends, in metres.</summary>
        public double Distance => _travel[_travel.Length - 1];

        /// <summary>From the start of the first prop (or the bike, if earlier) to the end of the last prop (or where the bike stops).</summary>
        public (double From, double To) Reach
        {
            get
            {
                double from = Math.Min(0.0, Props.Count == 0 ? 0.0 : Props.Min(p => p.X));
                double to = Math.Max(Distance, Props.Count == 0 ? 0.0 : Props.Max(p => p.X + p.Length));
                return (from, to);
            }
        }

        /// <summary>How far the bike has gone after <paramref name="seconds"/>, between frames as well.</summary>
        public double TravelAt(double seconds)
        {
            double f = seconds * FramesPerSecond;
            if (f <= 0) return _travel[0];
            int last = _travel.Length - 1;
            if (f >= last) return _travel[last];
            int i = (int)Math.Floor(f);
            double w = f - i;
            return _travel[i] + (_travel[i + 1] - _travel[i]) * w;
        }
    }

    /// <summary>
    /// The trick props and how each trick uses them, read from the props_layout.json file that
    /// Tools/Blender/build_unity_props.py writes next to the prop models.
    /// </summary>
    public sealed class TrickLayout
    {
        private readonly Dictionary<string, TrickProp> _props;
        private readonly Dictionary<string, TrickAnimation> _animations;

        private TrickLayout(double fps, Dictionary<string, TrickProp> props, Dictionary<string, TrickAnimation> animations)
        {
            FramesPerSecond = fps; _props = props; _animations = animations;
        }

        public double FramesPerSecond { get; }
        public IReadOnlyCollection<TrickProp> Props => _props.Values;
        public IReadOnlyCollection<TrickAnimation> Animations => _animations.Values;
        public IEnumerable<string> TrickNames => _animations.Keys;

        public TrickProp? Prop(string name) => _props.TryGetValue(name, out var p) ? p : null;

        public TrickAnimation? Find(string name) => _animations.TryGetValue(name, out var a) ? a : null;

        /// <summary>Reads the layout. Throws <see cref="FormatException"/> with a plain explanation if the file is not right.</summary>
        public static TrickLayout Parse(string json)
        {
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Newtonsoft.Json.JsonException ex) { throw new FormatException("The trick layout is not valid JSON: " + ex.Message, ex); }

            string units = (string?)root["units"] ?? "metres";
            if (units != "metres") throw new FormatException("The trick layout must be in metres, not " + units + ".");
            double fps = Number(root["fps"], "fps");
            if (fps <= 0) throw new FormatException("The trick layout's frame rate must be above zero.");

            var props = new Dictionary<string, TrickProp>(StringComparer.Ordinal);
            if (root["props"] is JObject propList)
            {
                foreach (var entry in propList.Properties())
                {
                    if (!(entry.Value is JObject p)) throw new FormatException("Prop " + entry.Name + " is not described properly.");
                    props[entry.Name] = new TrickProp(entry.Name, (string?)p["kind"] ?? "dirt",
                        Number(p["length"], entry.Name + " length"), Number(p["width"], entry.Name + " width"), Number(p["height"], entry.Name + " height"));
                }
            }

            var animations = new Dictionary<string, TrickAnimation>(StringComparer.Ordinal);
            if (root["animations"] is JArray list)
            {
                foreach (var token in list)
                {
                    if (!(token is JObject a)) throw new FormatException("A trick in the layout is not described properly.");
                    string name = (string?)a["name"] ?? throw new FormatException("A trick in the layout has no name.");
                    int frames = (int)Number(a["frames"], name + " frames");
                    var travel = (a["travel"] as JArray)?.Select(t => Number(t, name + " travel")).ToArray()
                                 ?? throw new FormatException("Trick " + name + " has no travel distances.");
                    if (travel.Length != frames + 1)
                        throw new FormatException("Trick " + name + " has " + travel.Length + " travel distances for " + frames + " frames; it needs one more than the frames.");
                    var placements = new List<TrickPropPlacement>();
                    if (a["props"] is JArray used)
                    {
                        foreach (var u in used)
                        {
                            string prop = (string?)u["prop"] ?? throw new FormatException("Trick " + name + " uses a prop with no name.");
                            if (!props.ContainsKey(prop)) throw new FormatException("Trick " + name + " uses " + prop + ", which is not in the list of props.");
                            placements.Add(new TrickPropPlacement(prop, Number(u["x"], name + " prop position"), Number(u["length"], name + " prop length")));
                        }
                    }
                    if (animations.ContainsKey(name)) throw new FormatException("Trick " + name + " is in the layout twice.");
                    animations[name] = new TrickAnimation(name, (string?)a["stage"] ?? "air", frames, fps, placements, travel);
                }
            }
            return new TrickLayout(fps, props, animations);
        }

        private static double Number(JToken? token, string what)
        {
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                throw new FormatException("The trick layout is missing a number for " + what + ".");
            double value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new FormatException("The trick layout has an impossible number for " + what + ": " + value.ToString(CultureInfo.InvariantCulture) + ".");
            return value;
        }
    }
}
