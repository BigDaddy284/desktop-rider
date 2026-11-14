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

        /// <summary>True while a trick is being ridden, or the rider is on its way to the start of one.</summary>
        public bool IsRunning => run != null || pending != null;

        /// <summary>The trick being ridden, or null.</summary>
        public string Current => run?.Animation.Name;

        /// <summary>Raised when a trick ends, with its name.</summary>
        public event Action<string> Finished;

        void Awake()
        {
            if (layoutFile == null) return;
            try
            {
                layout = TrickLayout.Parse(layoutFile.text);
            }
            catch (FormatException ex)
            {
                Debug.LogWarning("Desktop Rider's tricks are switched off because the trick layout could not be read. " + ex.Message);
            }
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
            Func<Numerics.Vector2, bool> fits = ground => FitsOnScreen(view, ground);
            float? heading = TrickRun.PickHeading(trick, here, rider.transform.eulerAngles.y, fits, halfWidth);
            if (heading != null) return Begin(trick, heading.Value);

            // No room here: ride to the nearest place with room, then go. A trick needs several metres of desktop.
            var plan = TrickRun.PlanStart(trick, here, rider.transform.eulerAngles.y, Candidates(view), fits, halfWidth);
            if (plan == null) return false;
            var (start, planned) = plan.Value;
            pending = trick;
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
            float? heading = TrickRun.PickHeading(trick, new Numerics.Vector2(at.x, at.z), planned, ground => FitsOnScreen(view, ground), HalfWidth(trick));
            if (heading == null || Mathf.Abs(TrickRun.TurnBetween(planned, heading.Value)) > 31f) return;   // no room after all: the rider just carries on
            Begin(trick, heading.Value);
        }

        float HalfWidth(TrickAnimation trick) =>
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
            rider.BeginTrick();
            rider.PlaceForTrick(at, heading);
            rider.Framing = FrameFor(view, run, halfWidth);
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
                rider.EndTrick();
                if (overlayWindow != null) OverlayWindow.Resize(overlayWindow.SizeFor(rider.SizeScale));
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
            var top = view.ToDesktop(new Vector3(ground.X, airRoom, ground.Y));
            return OverlayWindow.Area.IsOnScreen(new Numerics.Vector2(pixel.x, pixel.y), screenMargin)
                && OverlayWindow.Area.IsOnScreen(new Numerics.Vector2(top.x, top.y), 0f);
        }

        /// <summary>The part of the desktop the whole trick covers, props and the bike in the air included.</summary>
        Rect FrameFor(DesktopView view, TrickRun trickRun, float halfWidth)
        {
            var (from, to) = trickRun.Animation.Reach;
            var forward = trickRun.Forward;
            var side = new Numerics.Vector2(forward.Y, -forward.X) * halfWidth;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (double d in new[] { from - 1.0, to + 0.5 })
            {
                foreach (var s in new[] { side, -side })
                {
                    var g = trickRun.Start + forward * (float)d + s;
                    foreach (float height in new[] { 0f, airRoom })
                    {
                        var px = view.ToDesktop(new Vector3(g.X, height, g.Y));
                        minX = Mathf.Min(minX, px.x); maxX = Mathf.Max(maxX, px.x);
                        minY = Mathf.Min(minY, px.y); maxY = Mathf.Max(maxY, px.y);
                    }
                }
            }
            const float pad = 16f;
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
            int added = 0;
            foreach (AnimationState state in tricksModel)
            {
                if (state.clip == null || rider.modelAnimation[state.name] != null) continue;
                rider.modelAnimation.AddClip(state.clip, state.name);
                added++;
            }
            // The tricks model is only a store of clips; it is never shown.
            tricksModel.gameObject.SetActive(false);
            if (added == 0) Debug.LogWarning("Desktop Rider found no trick clips to add from " + tricksModel.name + ".");
        }
    }
}
