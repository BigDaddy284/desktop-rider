using System;
using DesktopRider.Overlay;
using UnityEngine;
using Numerics = System.Numerics;

namespace DesktopRider.Riding
{
    /// <summary>
    /// Moves the rider about the desktop. With <see cref="Roaming"/> on (the free-riding setting) the bike wanders over
    /// every screen by itself, leaning into turns; with it off the rider stays parked where it was put. Either way it can be
    /// picked up and dropped with the mouse. The steering rules live in <see cref="RiderSteering"/> so they can be tested.
    /// </summary>
    public sealed class FreeRider : MonoBehaviour
    {
        [Header("Scene")]
        public Camera viewCamera;
        public Transform leanPivot;
        public UnityEngine.Animation modelAnimation;
        [Tooltip("The rider's own riding style (resting, setting off, riding, corners); replaces the model's clips of the same name.")]
        public UnityEngine.Animation styleModel;

        [Header("Riding")]
        public float cruiseSpeed = 2.6f;          // metres a second
        public float acceleration = 2.5f;
        public float braking = 4.0f;
        public float maxTurnRate = 110f;          // degrees a second at cruise speed
        [Tooltip("How far the bike leans at most in a turn. The rider's body stays nearer upright (the corner clips).")]
        public float maxLean = 38f;
        public float arriveDistance = 0.6f;
        public Vector2 restSeconds = new Vector2(2f, 6f);
        [Range(0.02f, 0.3f)] public float screenMargin = 0.12f;
        [Tooltip("Chance that the next place to ride to is on another screen, when there is more than one.")]
        [Range(0f, 1f)] public float otherScreenChance = 0.25f;

        [Header("View")]
        [Tooltip("Pixels per metre at 100% screen scaling and normal rider size.")]
        public float pixelsPerMetre = 100f;
        [Tooltip("How steeply the camera looks down at the desktop, in degrees.")]
        public float cameraPitch = 45f;
        [Tooltip("The window is centred this far above the rider's feet, in metres.")]
        public float framingHeight = 0.7f;

        [Header("Frame rate")]
        public int movingFrameRate = 30;
        public int restingFrameRate = 10;

        /// <summary>True: rides about by itself. False: stays parked where it is.</summary>
        public bool Roaming
        {
            get => roaming;
            set
            {
                if (roaming == value) return;
                roaming = value;
                if (roaming) { restUntil = Time.time + restSeconds.x; PickTarget(); }
                else target = transform.position;
            }
        }

        /// <summary>Chooses the clip: riding while moving, otherwise rest, idle movements, talking and tricks as in version 1.</summary>
        public DesktopRider.Animation.ModelAnimationDirector Director { get; } = new DesktopRider.Animation.ModelAnimationDirector();

        /// <summary>Raised each time the bike sets off from a stop (for the dirt and smoke at the rear wheel).</summary>
        public event Action SetOff;

        /// <summary>Raised when the user lets go of the rider after dragging it.</summary>
        public event Action Dropped;

        /// <summary>Where the rider's wheels touch the desktop, in desktop pixels.</summary>
        public Vector2 DesktopPosition => View.ToDesktop(transform.position);

        /// <summary>True while a trick is being ridden; the trick moves the rider and plays its own clip.</summary>
        public bool InTrick { get; private set; }

        /// <summary>Raised when the user picks the rider up in the middle of a trick, so the trick can be packed away.</summary>
        public event Action TrickInterrupted;

        /// <summary>
        /// While set, the window covers this part of the desktop (in desktop pixels) instead of a small square around the
        /// rider, so ramps and landings ahead of the bike are not cut off.
        /// </summary>
        public Rect? Framing { get; set; }

        /// <summary>True while riding to the start of a trick.</summary>
        public bool Approaching => approachArrived != null;

        /// <summary>The rider size setting (1 is normal).</summary>
        public float SizeScale => sizeScale;

        /// <summary>How the ground maps to desktop pixels at the moment.</summary>
        public DesktopView CurrentView => View;

        Vector3 target, approachEnd;
        bool approachLeg;                     // true while heading for the run-up point, false on the last stretch
        Action approachArrived;
        float speed, restUntil, lean, turnAhead, sizeScale = 1f;
        readonly CornerBlend corner = new CornerBlend();
        bool roaming = true, dragging;
        Vector3 dragOffset;
        int playedStarts = -1;
        float animationSpeed = 1f;
        readonly System.Random random = new System.Random();

        void Start()
        {
            QualitySettings.vSyncCount = 0;
            // Each extra worker thread spins while waiting for work; one is plenty for a single rider.
            // Measured on the development PC: 26.8 % of a core with the default count, 19.6 % with one.
            Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount = CommandLineInt("-workers", 1);
            // For measuring: "-movingFps 30 -restingFps 10" on the command line overrides the frame rates.
            movingFrameRate = CommandLineInt("-movingFps", movingFrameRate);
            restingFrameRate = CommandLineInt("-restingFps", restingFrameRate);
            if (viewCamera == null) viewCamera = Camera.main;
            if (styleModel != null && modelAnimation != null)
            {
                ClipLibrary.Copy(styleModel, modelAnimation, replace: true);
                if (modelAnimation["Idle"] != null) modelAnimation.Play("Idle");
            }
            OverlayWindow.ScreensChanged += OnScreensChanged;
            if (roaming) PickTarget(); else target = transform.position;
        }

        void OnDestroy() => OverlayWindow.ScreensChanged -= OnScreensChanged;

        /// <summary>Puts the rider at a desktop pixel (kept on a screen), stopped, and lets it rest before riding on.</summary>
        public void MoveTo(Vector2 desktop)
        {
            var kept = OverlayWindow.Area.KeepOnScreen(new Numerics.Vector2(desktop.x, desktop.y));
            if (View.ToGround(new Vector2(kept.X, kept.Y), out Vector3 ground)) transform.position = ground;
            speed = 0f;
            restUntil = Time.time + restSeconds.x;
            if (roaming) PickTarget(); else target = transform.position;
        }

        /// <summary>
        /// Makes the rider bigger or smaller (1 is normal). The rider keeps its place on the desktop; only the drawing scale
        /// changes, so the window is resized to match.
        /// </summary>
        public void SetSizeScale(float scale)
        {
            scale = Mathf.Clamp(scale, 0.25f, 4f);
            if (Mathf.Approximately(scale, sizeScale)) return;
            var at = DesktopPosition;
            var aim = View.ToDesktop(target);
            sizeScale = scale;
            if (View.ToGround(at, out var ground)) transform.position = ground;
            if (View.ToGround(aim, out var newTarget)) target = newTarget;
        }

        /// <summary>Stops riding about so a trick can take over: no steering, no lean, no clip changes.</summary>
        public void BeginTrick()
        {
            InTrick = true;
            speed = 0f;
            lean = 0f;
            if (leanPivot != null) leanPivot.localRotation = Quaternion.identity;
            UpdateCornerPose(false);
        }

        /// <summary>Puts the rider on a ground point during a trick, facing <paramref name="heading"/> degrees.</summary>
        public void PlaceForTrick(Vector3 ground, float heading)
        {
            transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, heading, 0f));
        }

        /// <summary>
        /// Rides to the start of a trick, lining up to face <paramref name="heading"/>, then calls
        /// <paramref name="arrived"/>. Works parked or free riding; picking the rider up cancels it.
        /// </summary>
        public void RideTo(Vector3 start, float heading, Action arrived)
        {
            var forward = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            // Stop where the trick starts: the bike comes to rest arriveDistance short of its aim.
            approachEnd = start + forward * arriveDistance;
            target = start - forward * 1.2f;               // a run-up point behind the start, so it arrives facing the right way
            approachLeg = true;
            approachArrived = arrived;
            restUntil = 0f;
        }

        /// <summary>Stops riding to a trick's start, if it was.</summary>
        public void CancelApproach()
        {
            if (approachArrived == null) return;
            approachArrived = null;
            if (roaming) PickTarget(); else target = transform.position;
        }

        /// <summary>Hands the rider back after a trick: it rests a moment where it stopped, then carries on.</summary>
        public void EndTrick()
        {
            if (!InTrick) return;
            InTrick = false;
            Framing = null;
            speed = 0f;
            restUntil = Time.time + restSeconds.x;
            playedStarts = -1;                         // start the resting clip afresh
            if (roaming) PickTarget(); else target = transform.position;
        }

        void Update()
        {
            UpdateMouse();
            if (dragging && (InTrick || Approaching)) { CancelApproach(); TrickInterrupted?.Invoke(); }
            if (dragging) { Animate(false); UpdateCornerPose(false); SetFrameRate(movingFrameRate); return; }
            if (InTrick) { SetFrameRate(movingFrameRate); return; }

            bool resting = !Approaching && (!roaming || Time.time < restUntil);
            float distance = RiderSteering.FlatDistance(transform.position, target);
            if (Approaching && approachLeg && distance < arriveDistance + 0.4f)
            {
                // Past the run-up point: carry straight on to the start without stopping.
                approachLeg = false;
                target = approachEnd;
                distance = RiderSteering.FlatDistance(transform.position, target);
            }
            float wantedSpeed = resting ? 0f : RiderSteering.SpeedFor(distance, cruiseSpeed, braking, arriveDistance);
            speed = Mathf.MoveTowards(speed, wantedSpeed, (wantedSpeed > speed ? acceleration : braking) * Time.deltaTime);

            float turn = 0f;
            turnAhead = 0f;
            if (speed > 0.05f)
            {
                float heading = transform.eulerAngles.y;
                float wanted = RiderSteering.HeadingTo(transform.position, target);
                turnAhead = RiderSteering.ShortestTurn(heading, wanted);
                float rate = maxTurnRate * Mathf.Clamp01(speed / cruiseSpeed + 0.25f);
                turn = RiderSteering.TurnStep(heading, wanted, rate * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, heading + turn, 0f);
                transform.position += transform.forward * (speed * Time.deltaTime);
            }

            float turnRate = Time.deltaTime > 0f ? turn / Time.deltaTime : 0f;
            float wantedLean = RiderSteering.LeanFor(turnRate, speed, maxLean);
            lean = Mathf.Lerp(lean, wantedLean, 1f - Mathf.Exp(-8f * Time.deltaTime));
            if (leanPivot != null) leanPivot.localRotation = Quaternion.Euler(0f, 0f, -lean);

            if (Approaching && !approachLeg && distance < arriveDistance + 0.05f && speed < 0.15f)
            {
                var arrived = approachArrived;
                approachArrived = null;
                target = transform.position;
                arrived();
                return;
            }

            if (!Approaching && roaming && !resting && distance < arriveDistance && speed < 0.3f)
            {
                restUntil = Time.time + Mathf.Lerp(restSeconds.x, restSeconds.y, (float)random.NextDouble());
                PickTarget();
            }

            bool moving = speed > 0.05f;
            Animate(moving);
            UpdateCornerPose(moving);
            SetFrameRate(moving ? movingFrameRate : restingFrameRate);
        }

        float PixelsPerMetre => pixelsPerMetre * OverlayWindow.ScaleFactor * sizeScale;

        DesktopView View => new DesktopView(OverlayWindow.Area.Size.Width, OverlayWindow.Area.Size.Height, PixelsPerMetre, cameraPitch);

        /// <summary>After moving: put the small window over the rider and aim the camera so it shows the right part of the desktop.</summary>
        void LateUpdate()
        {
            if (viewCamera == null) return;
            var view = View;
            var window = OverlayWindow.WindowRect;
            if (OverlayWindow.IsActive && Framing is Rect frame)
            {
                // A window the shape of the whole trick, cut to the desktop (nothing shows past its edge anyway); most of
                // it is see-through and lets clicks pass. A square one was taller than the screen for long jumps, and
                // Windows then cut the window short, which cut off the front wheel or the helmet.
                var fit = TrickWindow(frame, OverlayWindow.Area.Size.Width, OverlayWindow.Area.Size.Height);
                if (window.width != fit.width || window.height != fit.height) OverlayWindow.Resize(fit.width, fit.height);
                OverlayWindow.PlaceWindow(fit.position);
                window = OverlayWindow.WindowRect;
            }
            else if (OverlayWindow.IsActive)
            {
                var centre = view.ToDesktop(transform.position + Vector3.up * framingHeight);
                var corner = OverlayWindow.Area.WindowCorner(new Numerics.Vector2(centre.x, centre.y),
                    new DesktopRider.Core.Placement.PixelSize(window.width, window.height));
                OverlayWindow.PlaceWindow(new Vector2Int(corner.X, corner.Y));
                window = OverlayWindow.WindowRect;
            }

            var windowCentre = new Vector2(window.x + window.width * 0.5f, window.y + window.height * 0.5f);
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = window.height / (2f * view.PixelsPerMetre);
            viewCamera.transform.SetPositionAndRotation(view.CameraFor(windowCentre), view.Rotation);
        }

        /// <summary>The window for a trick: the trick's frame in whole pixels, kept inside the desktop.</summary>
        public static RectInt TrickWindow(Rect frame, int desktopWidth, int desktopHeight)
        {
            int left = Mathf.Max(0, Mathf.FloorToInt(frame.xMin)), top = Mathf.Max(0, Mathf.FloorToInt(frame.yMin));
            int right = Mathf.Min(desktopWidth, Mathf.CeilToInt(frame.xMax)), bottom = Mathf.Min(desktopHeight, Mathf.CeilToInt(frame.yMax));
            return new RectInt(left, top, Mathf.Max(64, right - left), Mathf.Max(64, bottom - top));
        }

        void UpdateMouse()
        {
            if (viewCamera == null) return;
            var view = View;
            var window = OverlayWindow.WindowRect;
            Vector2 mouse = OverlayWindow.MouseDesktop;

            bool over = false;
            if (window.Contains(new Vector2Int((int)mouse.x, (int)mouse.y)))
            {
                var inWindow = new Vector3(mouse.x - window.x, window.height - (mouse.y - window.y), 0f);
                Ray ray = viewCamera.ScreenPointToRay(inWindow);
                over = Physics.Raycast(ray, out RaycastHit hit, 500f) && hit.transform.IsChildOf(transform);
            }
            OverlayWindow.WantsMouse = over || dragging;

            if (!dragging && over && OverlayWindow.LeftButtonDown && view.ToGround(mouse, out Vector3 start))
            {
                dragging = true;
                dragOffset = transform.position - start;
                speed = 0f;
            }
            else if (dragging)
            {
                if (!OverlayWindow.LeftButtonDown)
                {
                    dragging = false;
                    restUntil = Time.time + restSeconds.x;
                    if (roaming) PickTarget(); else target = transform.position;
                    Dropped?.Invoke();
                }
                else if (view.ToGround(mouse, out Vector3 point))
                {
                    transform.position = KeepOnDesktop(view, point + dragOffset);
                }
            }
        }

        /// <summary>Pulls a ground point back so the rider stays on a screen.</summary>
        static Vector3 KeepOnDesktop(DesktopView view, Vector3 ground)
        {
            var pixel = view.ToDesktop(ground);
            var kept = OverlayWindow.Area.KeepOnScreen(new Numerics.Vector2(pixel.x, pixel.y));
            return view.ToGround(new Vector2(kept.X, kept.Y), out var onScreen) ? onScreen : ground;
        }

        void OnScreensChanged()
        {
            // A screen may have gone; bring the rider back onto one that is still there.
            transform.position = KeepOnDesktop(View, transform.position);
            if (roaming) PickTarget(); else target = transform.position;
        }

        void PickTarget()
        {
            var view = View;
            var from = view.ToDesktop(transform.position);
            var pixel = OverlayWindow.Area.PickTarget(new Numerics.Vector2(from.x, from.y), random, screenMargin, otherScreenChance);
            if (view.ToGround(new Vector2(pixel.X, pixel.Y), out Vector3 point)) target = point;
        }

        /// <summary>Plays clips at this speed (1 is normal), for the animation-speed setting.</summary>
        public void SetAnimationSpeed(float speed)
        {
            animationSpeed = Mathf.Clamp(speed, 0.25f, 4f);
            Director.Speed = animationSpeed;
            if (modelAnimation == null) return;
            foreach (AnimationState state in modelAnimation) state.speed = animationSpeed;
        }

        void Animate(bool moving)
        {
            // FromTicks, not FromSeconds: Unity's FromSeconds rounds to whole milliseconds, which slows the clips slightly.
            Director.Advance(TimeSpan.FromTicks((long)(Time.deltaTime * TimeSpan.TicksPerSecond)), moving);
            if (Director.Starts == playedStarts || modelAnimation == null) return;
            playedStarts = Director.Starts;
            var state = modelAnimation[Director.Clip];
            if (state == null) return;
            // A clip that plays once starts from its beginning each time it is asked for.
            if (state.wrapMode != WrapMode.Loop) state.time = 0f;
            state.speed = animationSpeed;
            modelAnimation.CrossFade(Director.Clip, 0.25f);
            if (Director.Clip == DesktopRider.Animation.ModelAnimationDirector.StartClip) SetOff?.Invoke();
        }

        /// <summary>
        /// In a turn the rider sits in the corner pose: inside leg straight out ahead, head into the turn, body nearer
        /// upright than the bike. The corner clips are blended over the riding (or setting-off) clip on a layer of their
        /// own. <see cref="CornerBlend"/> decides how much: the leg comes out as the turn is lined up, not halfway round.
        /// </summary>
        void UpdateCornerPose(bool moving)
        {
            if (modelAnimation == null) return;
            var left = modelAnimation["CornerLeft"];
            var right = modelAnimation["CornerRight"];
            if (left == null || right == null) return;
            string clip = Director.Clip;
            bool on = moving && !InTrick && !dragging &&
                      (clip == DesktopRider.Animation.ModelAnimationDirector.RidingClip || clip == DesktopRider.Animation.ModelAnimationDirector.StartClip);
            var (toLeft, toRight) = corner.Update(on ? turnAhead : 0f, on ? lean : 0f, maxLean, Time.deltaTime, on);
            var riding = modelAnimation[DesktopRider.Animation.ModelAnimationDirector.RidingClip];
            // Keep the corner clips in step with the riding loop while it plays, so the wheels and the bob match.
            float? at = riding != null && riding.enabled && riding.weight > 0.5f ? riding.normalizedTime : (float?)null;
            SetCornerWeight(left, toLeft, at);
            SetCornerWeight(right, toRight, at);
        }

        void SetCornerWeight(AnimationState state, float weight, float? normalizedTime)
        {
            state.layer = 1;
            state.wrapMode = WrapMode.Loop;
            if (weight <= 0.01f)
            {
                state.weight = 0f;
                state.enabled = false;
                return;
            }
            state.enabled = true;
            state.weight = weight;
            if (normalizedTime is float t)
            {
                state.speed = 0f;                              // moved along with the riding loop instead
                state.normalizedTime = t;
            }
            else state.speed = animationSpeed;
        }

        static int CommandLineInt(string name, int fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == name && int.TryParse(args[i + 1], out int value) && value > 0) return value;
            return fallback;
        }

        static void SetFrameRate(int rate)
        {
            if (Application.targetFrameRate != rate) Application.targetFrameRate = rate;
        }
    }
}
