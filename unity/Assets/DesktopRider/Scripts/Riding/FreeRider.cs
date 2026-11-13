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

        [Header("Size on screen")]
        public float pixelsPerMetre = 100f;

        [Header("Frame rate")]
        public int movingFrameRate = 60;
        public int restingFrameRate = 15;

        Vector3 target;
        float speed, restUntil, lean;
        bool dragging;
        Vector3 dragOffset;
        string playing;
        readonly System.Random random = new System.Random();

        void Start()
        {
            QualitySettings.vSyncCount = 0;
            if (viewCamera == null) viewCamera = Camera.main;
            PickTarget();
        }

        void Update()
        {
            if (viewCamera != null && viewCamera.orthographic)
                viewCamera.orthographicSize = Screen.height / (2f * pixelsPerMetre);
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

        void UpdateMouse()
        {
            if (viewCamera == null) return;
            Ray ray = viewCamera.ScreenPointToRay(OverlayWindow.MousePosition);
            bool over = Physics.Raycast(ray, out RaycastHit hit, 500f) && hit.transform.IsChildOf(transform);
            OverlayWindow.WantsMouse = over || dragging;

            if (!dragging && over && OverlayWindow.LeftButtonDown && GroundPoint(ray, out Vector3 start))
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
                else if (GroundPoint(ray, out Vector3 point))
                {
                    transform.position = point + dragOffset;
                }
            }
        }

        void PickTarget()
        {
            if (viewCamera == null) return;
            float x = Mathf.Lerp(screenMargin, 1f - screenMargin, (float)random.NextDouble());
            float y = Mathf.Lerp(screenMargin, 1f - screenMargin, (float)random.NextDouble());
            if (GroundPoint(viewCamera.ViewportPointToRay(new Vector3(x, y, 0f)), out Vector3 point)) target = point;
        }

        static bool GroundPoint(Ray ray, out Vector3 point)
        {
            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float enter)) { point = ray.GetPoint(enter); return true; }
            point = default;
            return false;
        }

        void Play(string clip)
        {
            if (modelAnimation == null || clip == playing || modelAnimation.GetClip(clip) == null) return;
            modelAnimation.CrossFade(clip, 0.25f);
            playing = clip;
        }

        static void SetFrameRate(int rate)
        {
            if (Application.targetFrameRate != rate) Application.targetFrameRate = rate;
        }
    }
}
