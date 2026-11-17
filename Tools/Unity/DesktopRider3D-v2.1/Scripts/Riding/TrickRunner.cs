using System;
using System.Collections.Generic;
using System.Linq;
using DesktopRider.Overlay;
using DesktopRider.Tricks;
using UnityEngine;
using Numerics = System.Numerics;

namespace DesktopRider.Riding
{
    /// <summary>
    /// Rides a trick on the desktop. It puts the ramp and landing down ahead of the bike, turned so the whole trick stays
    /// on a screen, plays the trick's clip on the rider, and moves the rider forward by the distance the trick covers
    /// at each moment (the bones themselves stay in place). Afterwards the props are put away and the rider carries on.
    /// Which tricks exist, and where their props go, comes from props_layout.json, written with the props in Blender.
    /// </summary>
    public sealed class TrickRunner : MonoBehaviour
    {
        public FreeRider rider;
        public OverlayWindow overlayWindow;

        [Tooltip("The imported tricks model; its takes are added to the rider's clips. Leave empty if the rider's model already has them.")]
        public UnityEngine.Animation tricksModel;

        [Tooltip("props_layout.json from the prop export.")]
        public TextAsset layoutFile;

        [Tooltip("The ramp, landing and whoop models, named as in the layout file.")]
        public GameObject[] propModels = Array.Empty<GameObject>();

        [Tooltip("Turns the props to match the rider model (Blender's forward becomes Unity's forward).")]
        public float propYaw = 90f;

        [Tooltip("Pixels kept clear between a trick and the edge of the screen.")]
        public float screenMargin = 24f;

        [Tooltip("Room left above the props for the bike in the air, in metres.")]
        public float airRoom = 1.8f;

        public float crossFade = 0.15f;

        TrickLayout layout;
        TrickRun run;
        TrickAnimation pending;              // the trick the rider is riding to the start of
        readonly TrickPicker picker = new TrickPicker();
        readonly Dictionary<string, GameObject> props = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        readonly Dictionary<string, TrickExtent> extents = new Dictionary<string, TrickExtent>(StringComparer.Ordinal);
        float checkRoom, checkMargin;          // the room above and the extra edge margin of the trick being checked

        /// <summary>
        /// The room the named trick takes, measured from its clip the first time it is asked for (see <see cref="TrickExtents"/>).
        /// </summary>
        public TrickExtent ExtentOf(TrickAnimation trick)
        {
            if (extents.TryGetValue(trick.Name, out var known)) return known;
            var state = rider != null && rider.modelAnimation != null ? rider.modelAnimation[trick.Name] : null;
            var measured = state == null
                ? new TrickExtent(1.0f, (float)trick.Distance + 0.6f, airRoom, 0.6f)
                : TrickExtents.Measure(rider.modelAnimation.gameObject, state.clip, rider.leanPivot != null ? rider.leanPivot : rider.transform, trick.TravelAt);
            extents[trick.Name] = measured;
            return measured;
        }

        /// <summary>Uses the trick's measured room when checking whether it fits on a screen.</summary>
        void PrepareCheck(TrickAnimation trick)
        {
            var extent = ExtentOf(trick);
            checkRoom = Mathf.Max(airRoom, extent.Up);
            // The trick's own check covers the props and where the bike stops; the bike's nose reaches a little further.
            float beyond = Mathf.Max(0f, extent.Front - (float)trick.Reach.To);
            checkMargin = beyond * rider.CurrentView.PixelsPerMetre;
        }

        /// <summary>True while a trick is being ridden, or the rider is on its way to the start of one.</summary>
        public bool IsRunning => run != null || pending != null;

        /// <summary>The trick being ridden, or null.</summary>
        public string Current => run?.Animation.Name;

        /// <summary>Raised when a trick ends, with its name.</summary>
        public event Action<string> Finished;

        void Awake() => layout = ReadLayout(layoutFile);

        static TrickLayout ReadLayout(TextAsset file)
        {
            if (file == null) return null;
            try
            {
                return TrickLayout.Parse(file.text);
            }
            catch (FormatException ex)
            {
                Debug.LogWarning("Desktop Rider's tricks are switched off because the trick layout could not be read. " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Rides another character's tricks from now on (the "Who rides" setting): its layout and props. Any trick under
        /// way is stopped and its props are put away. The clips must already be on the rider's model.
        /// </summary>
        public void Use(TextAsset layoutFile, GameObject[] propModels, float propYaw)
        {
            Stop();
            foreach (var prop in props.Values) if (prop != null) Destroy(prop);
            props.Clear();
            extents.Clear();
            this.layoutFile = layoutFile;
            this.propModels = propModels ?? Array.Empty<GameObject>();
            this.propYaw = propYaw;
            layout = ReadLayout(layoutFile);
            hasTricks = Available.Any();
        }

        void Start()
        {
            if (rider != null) rider.TrickInterrupted += Stop;
            CopyTrickClips();
            hasTricks = Available.Any();
        }

        void OnDestroy()
        {
            if (rider != null) rider.TrickInterrupted -= Stop;
        }

        /// <summary>
        /// Whether any trick can be ridden. Read from the tray menu's thread, so it only looks at what was found at start-up.
        /// </summary>
        public bool HasTricks => hasTricks;

        volatile bool hasTricks;

        /// <summary>The tricks that can be ridden: in the layout, with a clip on the rider and every prop present.</summary>
        public IEnumerable<TrickAnimation> Available
        {
            get
            {
                if (layout == null || rider == null || rider.modelAnimation == null) return Array.Empty<TrickAnimation>();
                var have = new HashSet<string>(propModels.Where(p => p != null).Select(p => p.name), StringComparer.Ordinal);
                return layout.Animations.Where(a => rider.modelAnimation[a.Name] != null && a.Props.All(p => have.Contains(p.Prop))).ToArray();
            }
        }

        /// <summary>
        /// Starts a trick: the one named, or one picked at random. Returns false (and nothing happens) if a trick is
        /// already running, the rider is being held, the trick does not exist or there is no room for it on any screen.
        /// </summary>
        public bool TryPerform(string trickName = null)
        {
            if (IsRunning || rider == null || !rider.isActiveAndEnabled || rider.InTrick || rider.Approaching) return false;
            var available = Available.ToList();
            string name = string.IsNullOrEmpty(trickName) ? picker.Next(available) : trickName;
            var trick = available.FirstOrDefault(a => a.Name == name);
            if (trick == null) return false;

            var at = rider.transform.position;
            var here = new Numerics.Vector2(at.x, at.z);
            var view = rider.CurrentView;
            float halfWidth = HalfWidth(trick);
            PrepareCheck(trick);
            Func<Numerics.Vector2, bool> fits = ground => FitsOnScreen(view, ground);
            float? heading = TrickRun.PickHeading(trick, here, rider.transform.eulerAngles.y, fits, halfWidth);
            if (heading != null) return Begin(trick, heading.Value);

            // No room here: ride to the nearest place with room, then go. A trick needs several metres of desktop.
            var plan = TrickRun.PlanStart(trick, here, rider.transform.eulerAngles.y, Candidates(view), fits, halfWidth);
            if (plan == null) return false;
            var (start, planned) = plan.Value;
            pending = trick;
            Debug.Log("Trick " + trick.Name + ": riding to a place with room first.");
            rider.RideTo(new Vector3(start.X, at.y, start.Y), planned, () => StartWhereArrived(planned));
            return true;
        }

        /// <summary>At the start point the bike stops near, not exactly on, the planned spot; check the room again from there.</summary>
        void StartWhereArrived(float planned)
        {
            var trick = pending;
            pending = null;
            if (trick == null || rider == null) return;
            var at = rider.transform.position;
            var view = rider.CurrentView;
            PrepareCheck(trick);
            float? heading = TrickRun.PickHeading(trick, new Numerics.Vector2(at.x, at.z), planned, ground => FitsOnScreen(view, ground), HalfWidth(trick));
            if (heading == null || Mathf.Abs(TrickRun.TurnBetween(planned, heading.Value)) > 31f)
            {
                Debug.Log("Trick " + trick.Name + " given up at its start: " + (heading == null ? "no room there after all" : "facing too far off") + ".");
                return;   // no room after all: the rider just carries on
            }
            Begin(trick, heading.Value);
        }

        float HalfWidth(TrickAnimation trick) => HalfWidth(layout, trick);

        /// <summary>Half the width of the widest prop the trick uses (or of the bike, with none).</summary>
        public static float HalfWidth(TrickLayout layout, TrickAnimation trick) =>
            trick.Props.Count == 0 ? 0.6f : 0.5f * (float)trick.Props.Max(p => layout.Prop(p.Prop)?.Width ?? 1.2);

        /// <summary>Places the rider could ride to: a grid over every screen, on the ground.</summary>
        IEnumerable<Numerics.Vector2> Candidates(DesktopView view)
        {
            const int spacing = 80;
            foreach (var screen in OverlayWindow.Area.Screens)
            {
                for (int y = screen.Y + spacing; y < screen.Y + screen.Height - spacing / 2; y += spacing)
                    for (int x = screen.X + spacing; x < screen.X + screen.Width - spacing / 2; x += spacing)
                        if (view.ToGround(new Vector2(x, y), out var ground))
                            yield return new Numerics.Vector2(ground.x, ground.z);
            }
        }

        bool Begin(TrickAnimation trick, float heading)
        {
            var at = rider.transform.position;
            var start = new Numerics.Vector2(at.x, at.z);
            var view = rider.CurrentView;
            float halfWidth = HalfWidth(trick);
            run = new TrickRun(trick, start, heading) { Speed = rider.Director.Speed };
            Debug.Log("Trick " + trick.Name + " begins at " + rider.DesktopPosition + ", heading " + heading.ToString("0") + ".");
            rider.BeginTrick();
            rider.PlaceForTrick(at, heading);
            rider.Framing = FrameFor(view, ExtentOf(trick), trick, at, heading, halfWidth);
            PlaceProps(run);
            PlayClip(trick.Name);
            return true;
        }

        /// <summary>Ends the trick now (also when the user picks the rider up) and puts the props away.</summary>
        public void Stop()
        {
            if (pending != null)
            {
                pending = null;
                rider?.CancelApproach();
            }
            if (run == null) return;
            string name = run.Animation.Name;
            run = null;
            foreach (var prop in props.Values) prop.SetActive(false);
            if (rider != null)
            {
                rider.EndTrick();         // the window keeps its trick size: shrinking it flickers (OverlayWindow.Grow)
            }
            Finished?.Invoke(name);
        }

        void Update()
        {
            // Moved elsewhere (for example "move back to the middle") on the way to a trick: forget it.
            if (pending != null && rider != null && !rider.Approaching && !rider.InTrick) pending = null;
            if (run == null) return;
            run.Speed = rider.Director.Speed;
            run.Advance(Time.deltaTime);
            var p = run.Position;
            rider.PlaceForTrick(new Vector3(p.X, rider.transform.position.y, p.Y), run.Heading);
            if (run.IsFinished) Stop();
        }

        bool FitsOnScreen(DesktopView view, Numerics.Vector2 ground)
        {
            var pixel = view.ToDesktop(new Vector3(ground.X, 0f, ground.Y));
            var top = view.ToDesktop(new Vector3(ground.X, checkRoom > 0f ? checkRoom : airRoom, ground.Y));
            return OverlayWindow.Area.IsOnScreen(new Numerics.Vector2(pixel.x, pixel.y), screenMargin + checkMargin)
                && OverlayWindow.Area.IsOnScreen(new Numerics.Vector2(top.x, top.y), 0f);
        }

        /// <summary>
        /// The part of the desktop the whole trick covers: the props, and the bike and rider at every moment of the clip
        /// (measured, so the front wheel at the end and the helmet at the top of the jump are inside).
        /// </summary>
        public static Rect FrameFor(DesktopView view, TrickExtent extent, TrickAnimation trick, Vector3 start, float heading, float halfWidth)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var corner in TrickExtents.Corners(extent, start, heading, halfWidth))
            {
                var px = view.ToDesktop(corner);
                minX = Mathf.Min(minX, px.x); maxX = Mathf.Max(maxX, px.x);
                minY = Mathf.Min(minY, px.y); maxY = Mathf.Max(maxY, px.y);
            }
            // The props too, which can reach further than the bike (a landing's run-out).
            var (from, to) = trick.Reach;
            var forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            var right = Quaternion.Euler(0f, heading, 0f) * Vector3.right;
            foreach (double d in new[] { from, to })
                foreach (float s in new[] { -halfWidth, halfWidth })
                {
                    var px = view.ToDesktop(start + forward * (float)d + right * s);
                    minX = Mathf.Min(minX, px.x); maxX = Mathf.Max(maxX, px.x);
                    minY = Mathf.Min(minY, px.y); maxY = Mathf.Max(maxY, px.y);
                }
            const float pad = 12f;
            return Rect.MinMaxRect(minX - pad, minY - pad, maxX + pad, maxY + pad);
        }

        void PlaceProps(TrickRun trickRun)
        {
            foreach (var prop in props.Values) prop.SetActive(false);
            foreach (var placed in trickRun.Props)
            {
                var instance = PropInstance(placed.Name);
                if (instance == null) continue;
                instance.transform.SetPositionAndRotation(new Vector3(placed.Position.X, 0f, placed.Position.Y),
                    Quaternion.Euler(0f, placed.Heading + propYaw, 0f));
                instance.SetActive(true);
            }
        }

        GameObject PropInstance(string name)
        {
            if (props.TryGetValue(name, out var existing)) return existing;
            var model = propModels.FirstOrDefault(p => p != null && p.name == name);
            if (model == null) return null;
            // Props are not children of the rider: they stay put on the ground while the bike rides over them.
            var instance = Instantiate(model);
            instance.name = name;
            instance.SetActive(false);
            props[name] = instance;
            return instance;
        }

        void PlayClip(string clip)
        {
            var animation = rider.modelAnimation;
            var state = animation[clip];
            if (state == null) return;
            state.wrapMode = WrapMode.ClampForever;   // hold the last pose until the rider takes over again
            state.time = 0f;
            state.speed = (float)rider.Director.Speed;
            animation.CrossFade(clip, crossFade);
        }

        /// <summary>The tricks are a separate model with the same bones; their takes are added to the rider's own clips.</summary>
        void CopyTrickClips()
        {
            if (tricksModel == null || rider == null || rider.modelAnimation == null || tricksModel == rider.modelAnimation) return;
            if (ClipLibrary.Copy(tricksModel, rider.modelAnimation, replace: false) == 0)
                Debug.LogWarning("Desktop Rider found no trick clips to add from " + tricksModel.name + ".");
        }
    }
}
