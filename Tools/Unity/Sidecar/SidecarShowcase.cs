using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DesktopRider.Sidecar
{
    /// <summary>
    /// The sidecar viewer: press Play, then pick a move from the panel on the left. Drag with the mouse to look round,
    /// use the scroll wheel to zoom. "Ride round" shows the turning moves (passenger hang-out and swing-over) as part of
    /// normal riding; "Play everything" runs through every move in turn.
    /// </summary>
    public sealed class SidecarShowcase : MonoBehaviour
    {
        public UnityEngine.Animation model;
        public TextAsset manifest;
        public GameObject[] propModels;
        public Camera viewCamera;

        [Serializable] public class Prop { public string prop; public float x; public float length; }
        [Serializable] public class Info { public string note; }
        [Serializable] public class Clip { public string name; public int frames; public bool loop; public string kind; public Prop[] props; public float travel; public Info info; }
        [Serializable] class Manifest { public string name; public int fps; public float ride_speed = 3f; public float turn_radius = 3.2f; public Clip[] clips; }

        static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            {"Idle","Waiting"},{"LookAround","Looking around"},{"Revving","Revving"},{"Thinking","Thinking"},{"Talking","Talking"},
            {"Celebration","Celebrating"},{"SleepStart","Nodding off"},{"Sleeping","Asleep"},{"SleepEnd","Waking up"},
            {"RideOff","Riding off"},{"RideOn","Pulling up"},{"RidingLoop","Riding along"},
            {"CornerLeft","Turning towards the chair (passenger hangs out)"},{"CornerRight","Turning away from the chair (passenger swings over)"},
            {"CornerLeftIn","Into a chair-side turn"},{"CornerLeftOut","Out of a chair-side turn"},{"CornerRightIn","Into a bike-side turn"},{"CornerRightOut","Out of a bike-side turn"},
            {"FlyingChair","Flying the chair"},{"Wheelie","Wheelie"},{"PowerSlide","Power slide"},{"PassengerWave","Passenger waves"},
            {"LostPassenger","Lost passenger!"},{"Rollers","Rollers"},{"Tabletop","Jump: Tabletop"},{"NoHander","Jump: No-hander"},
            {"PassengerSuperman","Jump: Passenger Superman"},{"NacNac","Jump: Nac-nac"},
        };
        static readonly (string title, string[] kinds)[] Groups =
        {
            ("Everyday", new[] {"everyday"}), ("Riding", new[] {"riding"}), ("Tricks", new[] {"trick", "ground"}), ("Jumps", new[] {"jump"}),
        };

        Manifest data;
        readonly Dictionary<string, Clip> byName = new Dictionary<string, Clip>();
        Transform rootBone, chassisBone;
        Vector3 forwardLocal = Vector3.forward;     // the outfit's forward direction in the model's own space
        readonly List<GameObject> shownProps = new List<GameObject>();
        Coroutine running;
        string now = "", nowNote = "";
        float speed = 1f;
        float yaw = 215f, pitch = 22f, distance = 5.5f;
        Vector2 scroll;
        Vector3 homePosition; Quaternion homeRotation;

        void Start()
        {
            data = JsonUtility.FromJson<Manifest>(manifest.text);
            foreach (var c in data.clips) byName[c.name] = c;
            foreach (var t in model.GetComponentsInChildren<Transform>())
            {
                if (t.name == "root" && t.parent != null && t.parent.name == "sidecar_rig") rootBone = t;
                if (t.name == "chassis" && t.parent == rootBone) chassisBone = t;
            }
            if (chassisBone == null) foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "chassis") chassisBone = t;
            MeasureForward();
            homePosition = model.transform.position; homeRotation = model.transform.rotation;
            Play("Idle");
        }

        /// <summary>Which way the outfit travels in the model's space: sampled from the start and end of a clip that moves.</summary>
        void MeasureForward()
        {
            var moving = data.clips.FirstOrDefaultTravel();
            if (moving == null || rootBone == null || model[moving.name] == null) return;
            var clip = model[moving.name].clip;
            clip.SampleAnimation(model.gameObject, 0f);
            var a = model.transform.InverseTransformPoint(rootBone.position);
            clip.SampleAnimation(model.gameObject, clip.length);
            var b = model.transform.InverseTransformPoint(rootBone.position);
            if ((b - a).sqrMagnitude > 0.01f) forwardLocal = (b - a).normalized;
        }

        /// <summary>Plays one move by its clip name (also used by the editor's checks).</summary>
        public void Play(string name)
        {
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(PlayOne(name, false));
        }

        IEnumerator PlayOne(string name, bool partOfSequence)
        {
            if (!byName.TryGetValue(name, out var c) || model[name] == null) yield break;
            ClearProps();
            Label(c);
            PlaceProps(c);
            var state = model[name];
            state.speed = speed;
            model.Stop(); model.Play(name);
            if (c.loop)
            {
                if (!partOfSequence) yield break;
                yield return new WaitForSeconds(3f / Mathf.Max(0.1f, speed));
                yield break;
            }
            yield return new WaitForSeconds(state.length / Mathf.Max(0.1f, speed));
            // stay where the move finished
            model.transform.position += model.transform.TransformDirection(forwardLocal) * c.travel;
            bool moving = c.travel > 0.01f;
            model.Play(moving && c.kind == "riding" && name == "RideOff" ? "RidingLoop" : "Idle");
            if (!partOfSequence) { yield return new WaitForSeconds(1.5f); ClearProps(); }
        }

        /// <summary>Starts the riding-round demo (also used by the editor's checks).</summary>
        public void StartRideRound() { if (running != null) StopCoroutine(running); running = StartCoroutine(RideRound(2)); }

        /// <summary>The move showing now, as the panel names it.</summary>
        public string Now => now;

        /// <summary>The moves listed in the manifest (also read by the editor's Move Viewer).</summary>
        public Clip[] ManifestClips() { if (data == null) data = JsonUtility.FromJson<Manifest>(manifest.text); return data.clips; }

        /// <summary>The plain name shown for a move.</summary>
        public static string LabelFor(string clip) => Labels.TryGetValue(clip, out var l) ? l : clip;

        /// <summary>The outfit's forward direction in the model's space (measured when needed).</summary>
        public Vector3 ForwardLocal()
        {
            if (data == null) data = JsonUtility.FromJson<Manifest>(manifest.text);
            if (rootBone == null) foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "root" && t.parent != null && t.parent.name == "sidecar_rig") rootBone = t;
            if (forwardLocal == Vector3.forward) MeasureForward();
            return forwardLocal;
        }

        /// <summary>Where things are (for the editor's checks).</summary>
        public string Report()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("model " + model.transform.position + " forward " + model.transform.TransformDirection(forwardLocal) + " chassis " + (chassisBone ? chassisBone.position.ToString() : "-"));
            foreach (var p in shownProps) if (p != null) sb.Append("; prop " + p.name + " at " + p.transform.position);
            return sb.ToString();
        }

        IEnumerator PlayAll()
        {
            foreach (var c in data.clips)
            {
                if (c.name.StartsWith("Corner")) continue;          // shown by "Ride round"
                Home();
                yield return PlayOne(c.name, true);
                yield return new WaitForSeconds(0.6f);
            }
            yield return RideRound(1);
            Home(); model.Play("Idle"); Label(byName["Idle"]);
        }

        /// <summary>Riding a figure of eight: straight, a turn towards the chair, straight, a turn away from the chair.</summary>
        IEnumerator RideRound(int laps)
        {
            ClearProps(); Home();
            float v = data.ride_speed > 0 ? data.ride_speed : 3f, radius = data.turn_radius > 0 ? data.turn_radius : 3.2f;   // riding speed and turn size from the manifest
            var plan = new List<(string clip, float seconds, float turn)>();
            for (int i = 0; i < laps; i++)
            {
                plan.Add(("RidingLoop", 1.2f, 0f)); plan.Add(("CornerLeftIn", 16f / 24f, 0.5f)); plan.Add(("CornerLeft", Mathf.PI * radius * 1.5f / v, 1f)); plan.Add(("CornerLeftOut", 16f / 24f, 0.5f));
                plan.Add(("RidingLoop", 1.2f, 0f)); plan.Add(("CornerRightIn", 16f / 24f, -0.5f)); plan.Add(("CornerRight", Mathf.PI * radius * 1.5f / v, -1f)); plan.Add(("CornerRightOut", 16f / 24f, -0.5f));
            }
            plan.Add(("RidingLoop", 1.0f, 0f));
            foreach (var (clip, seconds, turn) in plan)
            {
                if (model[clip] == null) continue;
                Label(byName[clip]);
                if (clip.EndsWith("In") || clip.EndsWith("Out")) model.Play(clip); else model.CrossFade(clip, 0.15f);
                float t = 0;
                while (t < seconds)
                {
                    float dt = Time.deltaTime * speed; t += dt;
                    var fwd = model.transform.TransformDirection(forwardLocal);
                    model.transform.position += fwd * v * dt;
                    // positive turn = towards the chair (the outfit's left)
                    model.transform.Rotate(model.transform.up, -turn * Mathf.Rad2Deg * v / radius * dt * Handedness(), Space.World);
                    yield return null;
                }
            }
        }

        /// <summary>Unity turns clockwise (seen from above) for a positive angle about up, so a left turn is negative.</summary>
        static float Handedness() => 1f;

        void Home()
        {
            model.transform.SetPositionAndRotation(homePosition, homeRotation);
        }

        void Label(Clip c)
        {
            now = Labels.TryGetValue(c.name, out var l) ? l : c.name;
            nowNote = c.info != null && !string.IsNullOrEmpty(c.info.note) ? c.info.note : "";
        }

        void PlaceProps(Clip c)
        {
            if (c.props == null) return;
            foreach (var p in c.props)
            {
                var source = Array.Find(propModels, m => m != null && m.name == p.prop);
                if (source == null) continue;
                var prop = Instantiate(source, model.transform.position + model.transform.TransformDirection(forwardLocal) * p.x, model.transform.rotation);
                prop.transform.localScale = model.transform.lossyScale;
                shownProps.Add(prop);
            }
        }

        void ClearProps()
        {
            foreach (var p in shownProps) if (p != null) Destroy(p);
            shownProps.Clear();
        }

        void LateUpdate()
        {
            if (viewCamera == null) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                var pos = mouse.position.ReadValue();
                bool overPanel = pos.x < 306;
                var delta = mouse.delta.ReadValue();
                if ((mouse.leftButton.isPressed && !overPanel) || mouse.rightButton.isPressed)
                {
                    yaw += delta.x * 0.3f; pitch = Mathf.Clamp(pitch - delta.y * 0.25f, -5f, 80f);
                }
                float wheel = mouse.scroll.ReadValue().y;
                if (!overPanel && Mathf.Abs(wheel) > 0.01f) distance = Mathf.Clamp(distance * (1f - Mathf.Sign(wheel) * 0.1f), 2f, 25f);
            }
            var target = chassisBone != null ? chassisBone.position + Vector3.up * 0.6f : model.transform.position;
            var rot = Quaternion.Euler(pitch, yaw, 0);
            viewCamera.transform.SetPositionAndRotation(target - rot * Vector3.forward * distance, rot);
        }

        void OnGUI()
        {
            if (data == null) return;
            GUILayout.BeginArea(new Rect(8, 8, 290, Screen.height - 16), GUI.skin.box);
            GUILayout.Label("<b>" + data.name + "</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 });
            GUILayout.Label("Now: " + now, new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Bold });
            if (nowNote.Length > 0) GUILayout.Label(nowNote, new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 11 });
            GUILayout.BeginHorizontal();
            GUILayout.Label("Speed", GUILayout.Width(45));
            speed = GUILayout.HorizontalSlider(speed, 0.1f, 1.5f);
            GUILayout.Label(speed.ToString("0.0") + "x", GUILayout.Width(36));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Ride round (turning, hang-out, swing-over)")) { if (running != null) StopCoroutine(running); running = StartCoroutine(RideRound(2)); }
            if (GUILayout.Button("Play everything")) { if (running != null) StopCoroutine(running); running = StartCoroutine(PlayAll()); }
            if (GUILayout.Button("Back to the start")) { if (running != null) StopCoroutine(running); ClearProps(); Home(); Play("Idle"); }
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var (title, kinds) in Groups)
            {
                GUILayout.Label("<b>" + title + "</b>", new GUIStyle(GUI.skin.label) { richText = true });
                foreach (var c in data.clips)
                {
                    if (Array.IndexOf(kinds, c.kind) < 0) continue;
                    if (c.name.EndsWith("In") || c.name.EndsWith("Out")) continue;
                    if (GUILayout.Button(Labels.TryGetValue(c.name, out var l) ? l : c.name)) { Home(); Play(c.name); }
                }
            }
            GUILayout.EndScrollView();
            GUILayout.Label("Drag to look round, scroll to zoom.", new GUIStyle(GUI.skin.label) { fontSize = 11 });
            GUILayout.EndArea();
        }
    }

    static class ClipListExtensions
    {
        public static SidecarShowcase.Clip FirstOrDefaultTravel(this SidecarShowcase.Clip[] clips)
        {
            foreach (var c in clips) if (c.travel > 1f) return c;
            return null;
        }
    }
}
