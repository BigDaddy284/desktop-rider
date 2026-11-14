#nullable enable

using System;
using System.Linq;
using System.Text;

namespace DesktopRider.Animation
{
    /// <summary>
    /// Decides which of the 3D model's clips plays, using the same rules as version 1's sprites: the resting loop, a
    /// small idle movement now and then, thinking and talking, and tricks asked for one after another. While the bike is
    /// moving, the riding loop plays instead, after the start clip whenever the bike sets off from a stop (feet down,
    /// then up on the pegs, as the rider does at the start gate). It only names clips; the Unity side plays them. It
    /// knows nothing about Unity.
    /// </summary>
    public sealed class ModelAnimationDirector
    {
        /// <summary>The clips in the 3D model, with their length in frames at 24 a second, as exported from Blender.</summary>
        public static readonly (string Name, int Frames, bool Loop)[] Clips =
        {
            ("Idle", 48, true), ("IdleVariation", 96, false), ("LookLeft", 32, false), ("LookRight", 32, false),
            ("Revving", 24, true), ("Thinking", 72, false), ("Talking", 24, true), ("Wheelie", 60, false),
            ("Celebration", 48, false), ("SleepStart", 24, false), ("Sleeping", 96, true), ("SleepEnd", 24, false),
            ("RideOff", 30, false), ("RidingLoop", 24, true), ("RideOn", 30, false),
        };

        public const double FramesPerSecond = 24;
        public const string RidingClip = "RidingLoop";

        /// <summary>Plays once each time the bike sets off from a stop, before the riding loop.</summary>
        public const string StartClip = "RideOff";

        /// <summary>The small movements picked now and then while resting, as in version 1.</summary>
        public static readonly string[] IdleChoices = { "IdleVariation", "LookLeft", "LookRight" };

        private readonly AnimationPlayer _player;
        private readonly CharacterBehaviour _behaviour;
        private readonly IdleVariationPicker _idle;
        private AnimationClip? _shown;
        private int _shownFrame;
        private bool _wasMoving;
        private bool _wasRolling;
        private TimeSpan _startLeft;
        private bool _idleWanted = true;

        public ModelAnimationDirector(Random? random = null)
        {
            Manifest = BuildManifest();
            _player = new AnimationPlayer(Manifest);
            _behaviour = new CharacterBehaviour(_player, Manifest);
            _idle = new IdleVariationPicker(_player, IdleChoices, random);
        }

        public SpriteManifest Manifest { get; }

        /// <summary>The clip that should be playing now.</summary>
        public string Clip { get; private set; } = "Idle";

        /// <summary>
        /// Goes up each time a clip should start from its beginning, including the same one-shot starting again,
        /// so the player can tell a fresh start from carrying on.
        /// </summary>
        public int Starts { get; private set; }

        /// <summary>Whether small idle movements play while resting (the idle-animations setting).</summary>
        public bool IdleAnimations
        {
            get => _idleWanted;
            set => _idleWanted = value;
        }

        /// <summary>Playback speed, where 1 is normal (the animation-speed setting).</summary>
        public double Speed
        {
            get => _player.Speed;
            set => _player.Speed = value;
        }

        /// <summary>Thinking, talking or resting, as the conversation goes.</summary>
        public CharacterState State
        {
            get => _behaviour.State;
            set => _behaviour.State = value;
        }

        /// <summary>True while a trick or gesture asked for is waiting or playing.</summary>
        public bool IsPerforming => _behaviour.IsPerforming;

        /// <summary>Asks for a clip to be played once (a loop is kept up for a few seconds). False if there is no such clip.</summary>
        public bool Perform(string clip) => _behaviour.Perform(clip);

        /// <summary>Moves time forward. <paramref name="moving"/> is true while the bike is rolling.</summary>
        public void Advance(TimeSpan elapsed, bool moving)
        {
            // Small idle movements are for a rider at rest, never mid-ride.
            _idle.Enabled = _idleWanted && !moving;
            _idle.Advance(elapsed);
            _behaviour.Advance(elapsed);

            var current = _player.Current;
            if (moving && !_behaviour.IsPerforming)
            {
                if (!_wasRolling)
                {
                    // Setting off from a stop: the start first, then the riding loop.
                    Clip = StartClip;
                    Starts++;
                    _startLeft = TimeSpan.FromTicks((long)(StartSeconds / Math.Max(0.05, Speed) * TimeSpan.TicksPerSecond));
                }
                else if (Clip == StartClip && _wasMoving)
                {
                    _startLeft -= elapsed;
                    if (_startLeft <= TimeSpan.Zero)
                    {
                        Clip = RidingClip;
                        Starts++;
                    }
                }
                else if (!_wasMoving || Clip != RidingClip)
                {
                    // Already rolling (for example after a trick): straight back to the riding loop.
                    Clip = RidingClip;
                    Starts++;
                }
            }
            else if (_wasMoving || current != _shown || (!current.Loop && _player.FrameIndex < _shownFrame))
            {
                // A new clip, the same one-shot starting over, or coming to a stop: start it from the beginning.
                Clip = current.Name;
                Starts++;
            }

            _shown = current;
            _shownFrame = _player.FrameIndex;
            _wasMoving = moving && !_behaviour.IsPerforming;
            _wasRolling = moving;
        }

        private static double StartSeconds => Clips.First(c => c.Name == StartClip).Frames / FramesPerSecond;

        private static SpriteManifest BuildManifest()
        {
            // The animation rules read a sprite list; the 3D model has no sheets, so each clip gets a placeholder name.
            var json = new StringBuilder();
            json.Append("{\"frameWidth\":1,\"frameHeight\":1,\"framesPerSecond\":").Append(FramesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\"groundY\":1,\"defaultAnimation\":\"Idle\",\"animations\":[");
            json.Append(string.Join(",", Clips.Select(c =>
            {
                string links = c.Name == "Sleeping" ? ",\"enter\":\"SleepStart\",\"exit\":\"SleepEnd\"" : string.Empty;
                return "{\"name\":\"" + c.Name + "\",\"loop\":" + (c.Loop ? "true" : "false") + ",\"frameCount\":" + c.Frames +
                       ",\"columns\":1,\"right\":\"3d\",\"left\":\"3d\"" + links + "}";
            })));
            json.Append("]}");
            return SpriteManifest.Parse(json.ToString());
        }
    }
}
