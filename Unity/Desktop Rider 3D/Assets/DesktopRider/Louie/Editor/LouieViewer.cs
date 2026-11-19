using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DesktopRider.Riding;
using DesktopRider.Tricks;
using UnityEditor;
using UnityEngine;

namespace LouieTools
{
    /// <summary>
    /// Desktop Rider > Louie > Louie Viewer: puts Louie (the second rider, on the electric bike) into the open scene beside
    /// Harvey and plays any of his clips: everyday moves, riding style and tricks (with Harvey's ramps and landings).
    /// Louie and the props are viewer-only objects (never saved into the scene), so Harvey's scene and models are untouched.
    /// Also answers Bridge/louie-command.txt ("id open", "id clip Name frame", "id shot Name frame", "id refresh", "id close")
    /// for automated checks; answers go to Bridge/louie-result-id.txt.
    /// </summary>
    [InitializeOnLoad]
    public sealed class LouieViewer : EditorWindow
    {
        static LouieViewer() { EditorApplication.update += BridgeHook.Poll; }

        public const string ModelPath = LouieImport.Folder + "Models/Louie.fbx";
        public const string TricksPath = LouieImport.Folder + "Models/Louie_Tricks.fbx";
        public const string StylePath = LouieImport.Folder + "Models/Louie_Style.fbx";
        const string HolderName = "Louie (viewer)";
        const int Fps = 24;
        static readonly Vector3 Beside = new Vector3(2.0f, 0f, 0f);     // where Louie stands, from Harvey

        static LouieViewer open;
        GameObject holder, model;
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        readonly List<GameObject> props = new List<GameObject>();
        string[] names = Array.Empty<string>(), labels = Array.Empty<string>();
        int chosen, frame;
        bool playing;
        double last;
        float speed = 1f;
        Vector3 start;
        TrickRunner runner;
        TrickLayout layout;
        string message = "";

        [MenuItem("Desktop Rider/Louie/Louie Viewer")]
        static void Open() => GetWindow<LouieViewer>("Louie").Show();

        void OnEnable() { open = this; EditorApplication.update += Tick; Load(); }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            ClearProps();
            if (holder != null) holder.transform.position = start;
            if (model != null && clips.TryGetValue("Idle", out var idle)) idle.SampleAnimation(model, 0f);
            if (open == this) open = null;
        }

        /// <summary>Finds or makes Louie in the open scene and reads his clips.</summary>
        public string Load()
        {
            clips.Clear(); names = Array.Empty<string>(); labels = Array.Empty<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null) { message = "Louie's model is not imported yet (" + ModelPath + ")."; return "ERROR " + message; }

            runner = FindFirstObjectByType<TrickRunner>(FindObjectsInactive.Include);
            layout = null;
            if (runner != null && runner.layoutFile != null)
                try { layout = TrickLayout.Parse(runner.layoutFile.text); } catch (Exception e) { Debug.LogWarning("Louie viewer: trick layout unreadable: " + e.Message); }

            var harvey = runner != null && runner.rider != null ? runner.rider.transform : null;
            start = (harvey != null ? new Vector3(harvey.position.x, 0f, harvey.position.z) : Vector3.zero) + Beside;
            holder = GameObject.Find(HolderName);
            if (holder == null)
            {
                holder = new GameObject(HolderName) { hideFlags = HideFlags.DontSave };
                model = (GameObject)Instantiate(prefab);
                model.name = "Model";
                model.hideFlags = HideFlags.DontSave;
                model.transform.SetParent(holder.transform, false);
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // Blender +X (the bike's nose) faces Unity +Z, as Harvey
            }
            else model = holder.transform.Find("Model")?.gameObject;
            holder.transform.SetPositionAndRotation(start, Quaternion.identity);
            if (model == null) { message = "Louie's model went missing; close and open the viewer again."; return "ERROR " + message; }

            var list = new List<(string name, string label)>();
            void Add(string path, string group, bool tricks)
            {
                foreach (var c in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>())
                {
                    if (c.name.StartsWith("__preview__")) continue;
                    string key = group + "/" + c.name;
                    clips[key] = c; list.Add((key, group + "/" + Nice(c.name)));
                }
            }
            Add(ModelPath, "Everyday", false);
            Add(StylePath, "Riding style", false);
            Add(TricksPath, "Tricks", true);
            names = list.Select(x => x.name).ToArray();
            labels = list.Select(x => x.label).ToArray();
            chosen = Mathf.Clamp(chosen, 0, Math.Max(0, names.Length - 1));
            message = names.Length == 0 ? "No clips found in Louie's files yet." : "";
            if (names.Length > 0) Choose(chosen);
            return "Louie at " + start + " (Harvey " + (harvey != null ? harvey.position.ToString() : "not found") + "); clips " + names.Length +
                   "; trick layout " + (layout != null ? "yes" : "no") + "; props " + (runner != null ? runner.propModels.Count(p => p != null) : 0);
        }

        static string Nice(string n) => System.Text.RegularExpressions.Regex.Replace(n, "(?<=[a-z])(?=[A-Z])", " ");

        AnimationClip Clip => names.Length == 0 ? null : clips[names[chosen]];
        bool IsTrick => names.Length > 0 && names[chosen].StartsWith("Tricks/");
        string ClipShortName => names.Length == 0 ? "" : names[chosen].Substring(names[chosen].IndexOf('/') + 1);
        TrickAnimation Trick => IsTrick && layout != null ? layout.Find(ClipShortName) : null;
        int Frames => Clip == null ? 0 : Mathf.RoundToInt(Clip.length * Fps);

        public string Choose(int i)
        {
            chosen = i; frame = 0; ClearProps();
            var trick = Trick;
            if (trick != null && runner != null)
            {
                var run = new TrickRun(trick, new System.Numerics.Vector2(start.x, start.z), 0f);
                foreach (var placed in run.Props)
                {
                    var source = runner.propModels.FirstOrDefault(p => p != null && p.name == placed.Name);
                    if (source == null) continue;
                    var prop = (GameObject)Instantiate(source);
                    prop.hideFlags = HideFlags.DontSave;
                    prop.transform.SetPositionAndRotation(new Vector3(placed.Position.X, 0f, placed.Position.Y), Quaternion.Euler(0f, placed.Heading + runner.propYaw, 0f));
                    props.Add(prop);
                }
            }
            ShowFrame();
            return "chose " + names[i] + " frames " + Frames + " props " + props.Count;
        }

        public bool ChooseByName(string clip)
        {
            int i = Array.FindIndex(names, n => n.EndsWith("/" + clip));
            if (i < 0) return false;
            Choose(i); return true;
        }

        void ClearProps() { foreach (var p in props) if (p != null) DestroyImmediate(p); props.Clear(); }

        public void SetFrame(int f) { frame = Mathf.Clamp(f, 0, Frames); ShowFrame(); }

        void ShowFrame()
        {
            if (model == null || holder == null) { Load(); if (model == null) return; }
            var clip = Clip;
            if (clip == null) return;
            float seconds = frame / (float)Fps;
            clip.SampleAnimation(model, Mathf.Min(seconds, clip.length));
            var trick = Trick;
            float along = trick != null ? (float)(trick.TravelAt(seconds) - trick.TravelAt(0)) : 0f;
            holder.transform.position = start + Vector3.forward * along;
            SceneView.RepaintAll(); Repaint();
        }

        void Tick()
        {
            if (!playing || EditorApplication.isPlaying || Clip == null) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - last < 1.0 / (Fps * speed)) return;
            last = now;
            frame = frame >= Frames ? 0 : frame + 1;
            ShowFrame();
        }

        public void FrameInSceneView()
        {
            if (model == null) return;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            foreach (var p in props) foreach (var r in p.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) sv.Frame(b, false);
        }

        void OnGUI()
        {
            if (EditorApplication.isPlaying) { EditorGUILayout.HelpBox("Stop Play to use the Louie viewer.", MessageType.Info); return; }
            if (names.Length == 0 || model == null)
            {
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(message) ? "Louie isn't loaded." : message, MessageType.Info);
                if (GUILayout.Button("Look again")) Load();
                return;
            }
            EditorGUILayout.LabelField("Louie stands to the right of Harvey. Pick a move and press Play.", EditorStyles.wordWrappedLabel);
            EditorGUI.BeginChangeCheck();
            int pick = EditorGUILayout.Popup("Move", chosen, labels);
            if (EditorGUI.EndChangeCheck()) Choose(pick);
            EditorGUI.BeginChangeCheck();
            int f = EditorGUILayout.IntSlider("Frame", frame, 0, Frames);
            if (EditorGUI.EndChangeCheck()) { playing = false; SetFrame(f); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ Previous")) { playing = false; SetFrame(frame - 1); }
                if (GUILayout.Button(playing ? "❚❚ Pause" : "▶ Play")) { playing = !playing; last = EditorApplication.timeSinceStartup; }
                if (GUILayout.Button("Next ▶")) { playing = false; SetFrame(frame + 1); }
            }
            speed = EditorGUILayout.Slider("Speed", speed, 0.1f, 1.5f);
            if (GUILayout.Button("Show Louie in the Scene view")) FrameInSceneView();
            EditorGUILayout.LabelField("Frame " + frame + " of " + Frames + (IsTrick && Trick == null ? " (no ramp layout found, so the trick plays on the spot)" : ""), EditorStyles.miniLabel);
        }

        // ---------------------------------------------------------------- automated checks
        static class BridgeHook
        {
            static double next;
            static readonly string Bridge = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Bridge");
            public static void Poll()
            {
                if (EditorApplication.timeSinceStartup < next) return;
                next = EditorApplication.timeSinceStartup + 1.0;
                string file = Path.Combine(Bridge, "louie-command.txt");
                if (!File.Exists(file) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                string[] parts;
                try { parts = File.ReadAllText(file).Trim().Split(new[] { ' ' }, 3); File.Delete(file); }
                catch (IOException) { return; }
                if (parts.Length < 2) return;
                string answer;
                try { answer = Run(parts[0], parts[1], parts.Length > 2 ? parts[2] : ""); }
                catch (Exception e) { answer = "ERROR " + e; }
                File.WriteAllText(Path.Combine(Bridge, "louie-result-" + parts[0] + ".txt"), answer);
            }

            static string Run(string id, string command, string arg)
            {
                switch (command)
                {
                    case "refresh": AssetDatabase.Refresh(); return "refreshed";
                    case "open":
                    {
                        var w = GetWindow<LouieViewer>("Louie"); w.Show();
                        var r = w.Load(); w.FrameInSceneView(); return r;
                    }
                    case "close": if (open != null) open.Close(); return "closed";
                    case "focus":
                    {
                        // "focus swingarm 0.35 -90": the Scene view looks at that part of Louie from the side, close up
                        if (open == null || open.model == null) return "ERROR the viewer is not open";
                        var w = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        var t = open.model.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == (w.Length > 0 ? w[0] : "swingarm"));
                        if (t == null) return "ERROR no part " + w[0];
                        float size = w.Length > 1 ? float.Parse(w[1], System.Globalization.CultureInfo.InvariantCulture) : 0.4f;
                        float yaw = w.Length > 2 ? float.Parse(w[2], System.Globalization.CultureInfo.InvariantCulture) : -90f;
                        var sv = SceneView.lastActiveSceneView;
                        if (sv == null) return "ERROR no Scene view open";
                        var point = t.position + (w.Length > 5 ? new Vector3(float.Parse(w[3], System.Globalization.CultureInfo.InvariantCulture), float.Parse(w[4], System.Globalization.CultureInfo.InvariantCulture), float.Parse(w[5], System.Globalization.CultureInfo.InvariantCulture)) : Vector3.zero);
                        sv.LookAt(point, Quaternion.Euler(10f, yaw, 0f), size);
                        sv.Focus(); sv.Repaint();
                        return "looking at " + t.name + " " + point;
                    }
                    case "clip":
                    case "shot":
                    {
                        if (open == null) return "ERROR the viewer is not open";
                        var words = arg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (words.Length > 0 && !open.ChooseByName(words[0])) return "ERROR no clip " + words[0];
                        if (words.Length > 1) open.SetFrame(int.Parse(words[1]));
                        var sb = new StringBuilder("showing " + open.names[open.chosen] + " frame " + open.frame + " of " + open.Frames + " props " + open.props.Count + " holder " + open.holder.transform.position);
                        if (command == "shot") sb.Append("\n" + Shot(open, Path.Combine(Bridge, "louie-shot-" + id + ".png"), words.Length > 2 ? words[2] : "side"));
                        return sb.ToString();
                    }
                    default: return "ERROR unknown command " + command;
                }
            }

            /// <summary>A picture of Louie (and any props) as they stand now, from the side or three-quarter rear, rendered on its own.</summary>
            static string Shot(LouieViewer v, string path, string view)
            {
                var preview = new PreviewRenderUtility();
                var copies = new List<GameObject>();
                try
                {
                    var rs = v.model.GetComponentsInChildren<Renderer>();
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    foreach (var src in new[] { v.holder }.Concat(v.props))
                    {
                        var c = Instantiate(src, src.transform.position, src.transform.rotation);
                        copies.Add(c); preview.AddSingleGO(c);
                    }
                    // the copy of the holder needs the same pose as the original
                    var clip = v.Clip; clip.SampleAnimation(copies[0].transform.Find("Model").gameObject, Mathf.Min(v.frame / (float)Fps, clip.length));
                    float yaw = view == "rear" ? -140f : view == "front" ? -40f : -90f;
                    var rot = Quaternion.Euler(view == "side" ? 5f : 18f, yaw, 0f);
                    float dist = Mathf.Max(4f, b.size.magnitude * 2.2f);
                    preview.camera.transform.SetPositionAndRotation(b.center - rot * Vector3.forward * dist, rot);
                    preview.camera.fieldOfView = 35f; preview.camera.nearClipPlane = 0.05f; preview.camera.farClipPlane = 200f;
                    preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(0.62f, 0.72f, 0.82f);
                    preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(50f, 140f, 0f);
                    preview.ambientColor = new Color(0.5f, 0.5f, 0.55f);
                    preview.BeginStaticPreview(new Rect(0, 0, 1000, 700));
                    preview.Render(true);
                    var tex = preview.EndStaticPreview();
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    DestroyImmediate(tex);
                    var sb = new StringBuilder("saved " + path + " bounds " + b.center + " size " + b.size);
                    foreach (var r in v.model.GetComponentsInChildren<SkinnedMeshRenderer>())
                        sb.Append("\n" + r.name + " materials " + r.sharedMaterials.Length + " texture " + (r.sharedMaterial != null && r.sharedMaterial.mainTexture != null ? r.sharedMaterial.mainTexture.name : "none"));
                    return sb.ToString();
                }
                finally { foreach (var c in copies) if (c != null) DestroyImmediate(c); preview.Cleanup(); }
            }
        }
    }
}
