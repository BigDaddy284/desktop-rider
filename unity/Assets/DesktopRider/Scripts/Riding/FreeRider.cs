using DesktopRider.Overlay;
using UnityEngine;

namespace DesktopRider.Riding
{
    /// <summary>
    /// Phase 1 proof: the bike wanders round the visible ground by itself, leaning into turns, and can be
    /// picked up and dropped with the mouse. The steering rules live in <see cref="RiderSteering"/> so they can be tested.
    /// </summary>
    public sealed class FreeRider : MonoBehaviour
    {
        [Header("Scene")]
        public Camera viewCamera;
        public Transform leanPivot;
        public Animation modelAnimation;
        public string ridingClip = "RidingLoop";
        public string idleClip = "Idle";

        [Header("Riding")]
        public float cruiseSpeed = 2.6f;          // metres a second
        public float acceleration = 2.5f;
        public float braking = 4.0f;
        public float maxTurnRate = 110f;          // degrees a second at cruise speed
        public float maxLean = 28f;
        public float arriveDistance = 0.6f;
        public Vector2 restSeconds = new Vector2(2f, 6f);
        [Range(0.02f, 0.3f)] public float screenMargin = 0.12f;

        [Header("View")]
        public float pixelsPerMetre = 100f;
        [Tooltip("How steeply the camera looks down at the desktop, in degrees.")]
        public float cameraPitch = 45f;
        [Tooltip("The window is centred this far above the rider's feet, in metres.")]
        public float framingHeight = 0.7f;

        [Header("Frame rate")]
        public int movingFrameRate = 30;
        public int restingFrameRate = 10;

        Vector3 target;
        float speed, restUntil, lean;
        bool dragging;
        Vector3 dragOffset;
        string playing;
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
            PickTarget();
        }

        void Update()
        {
            UpdateMouse();
            if (dragging) { Play(idleClip); SetFrameRate(movingFrameRate); return; }

            bool resting = Time.time < restUntil;
            float distance = RiderSteering.FlatDistance(transform.position, target);
            float wantedSpeed = resting ? 0f : RiderSteering.SpeedFor(distance, cruiseSpeed, braking, arriveDistance);
            speed = Mathf.MoveTowards(speed, wantedSpeed, (wantedSpeed > speed ? acceleration : braking) * Time.deltaTime);

            float turn = 0f;
            if (speed > 0.05f)
            {
                float heading = transform.eulerAngles.y;
                float wanted = RiderSteering.HeadingTo(transform.position, target);
                float rate = maxTurnRate * Mathf.Clamp01(speed / cruiseSpeed + 0.25f);
                turn = RiderSteering.TurnStep(heading, wanted, rate * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, heading + turn, 0f);
                transform.position += transform.forward * (speed * Time.deltaTime);
            }

            float turnRate = Time.deltaTime > 0f ? turn / Time.deltaTime : 0f;
            float wantedLean = RiderSteering.LeanFor(turnRate, speed, maxLean);
            lean = Mathf.Lerp(lean, wantedLean, 1f - Mathf.Exp(-8f * Time.deltaTime));
            if (leanPivot != null) leanPivot.localRotation = Quaternion.Euler(0f, 0f, -lean);

            if (!resting && distance < arriveDistance && speed < 0.3f)
            {
                restUntil = Time.time + Mathf.Lerp(restSeconds.x, restSeconds.y, (float)random.NextDouble());
                PickTarget();
            }

            bool moving = speed > 0.05f;
            Play(moving ? ridingClip : idleClip);
            SetFrameRate(moving ? movingFrameRate : restingFrameRate);
        }

        DesktopView View => new DesktopView(OverlayWindow.DesktopSize.x, OverlayWindow.DesktopSize.y, pixelsPerMetre, cameraPitch);

        /// <summary>After moving: put the small window over the rider and aim the camera so it shows the right part of the desktop.</summary>
        void LateUpdate()
        {
            if (viewCamera == null) return;
            var view = View;
            var window = OverlayWindow.WindowRect;
            if (OverlayWindow.IsActive)
            {
                var centre = view.ToDesktop(transform.position + Vector3.up * framingHeight);
                var corner = view.WindowCorner(centre, window.width, window.height);
                OverlayWindow.PlaceWindow(corner);
                window = OverlayWindow.WindowRect;
            }

            var windowCentre = new Vector2(window.x + window.width * 0.5f, window.y + window.height * 0.5f);
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = window.height / (2f * pixelsPerMetre);
            viewCamera.transform.SetPositionAndRotation(view.CameraFor(windowCentre), view.Rotation);
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
                    PickTarget();
                }
                else if (view.ToGround(mouse, out Vector3 point))
                {
                    transform.position = KeepOnDesktop(view, point + dragOffset);
                }
            }
        }

        /// <summary>Pulls a ground point back so the rider stays on the desktop.</summary>
        static Vector3 KeepOnDesktop(DesktopView view, Vector3 ground)
        {
            var pixel = view.ToDesktop(ground);
            pixel.x = Mathf.Clamp(pixel.x, 0f, view.Width);
            pixel.y = Mathf.Clamp(pixel.y, 0f, view.Height);
            return view.ToGround(pixel, out var kept) ? kept : ground;
        }

        void PickTarget()
        {
            var view = View;
            float x = Mathf.Lerp(screenMargin, 1f - screenMargin, (float)random.NextDouble()) * view.Width;
            float y = Mathf.Lerp(screenMargin, 1f - screenMargin, (float)random.NextDouble()) * view.Height;
            if (view.ToGround(new Vector2(x, y), out Vector3 point)) target = point;
        }

        void Play(string clip)
        {
            if (modelAnimation == null || clip == playing || modelAnimation.GetClip(clip) == null) return;
            modelAnimation.CrossFade(clip, 0.25f);
            playing = clip;
        }

        static int CommandLineInt(string name, int fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
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
