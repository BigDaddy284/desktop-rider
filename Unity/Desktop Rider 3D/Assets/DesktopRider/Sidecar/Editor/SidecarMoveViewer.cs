using System;
using System.Collections.Generic;
using System.Linq;
using DesktopRider.Sidecar;
using UnityEditor;
using UnityEngine;

namespace DesktopRider.Sidecar.EditorTools
{
    /// <summary>
    /// Desktop Rider > Sidecar > Move Viewer: pick any sidecar move from a list and play it or step through it frame by
    /// frame in the Scene view, without pressing Play (like Harvey's Trick Viewer). Works in the sidecar viewer scene;
    /// it puts the outfit back when closed.
    /// </summary>
    public sealed class SidecarMoveViewer : EditorWindow
    {
        SidecarShowcase show;
        string[] names = Array.Empty<string>(), labels = Array.Empty<string>();
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
        readonly List<GameObject> props = new List<GameObject>();
        int chosen, frame;
        bool playing;
        double last;
        float speed = 1f;
        const int Fps = 24;

        [MenuItem("Desktop Rider/Sidecar/Move Viewer")]
        static void Open() => GetWindow<SidecarMoveViewer>("Sidecar Moves").ShowFrame();

        void OnEnable() { Load(); EditorApplication.update += Tick; EditorApplication.projectChanged += Reload; EditorSceneManagerHook(true); }
        void OnDisable() { EditorApplication.update -= Tick; EditorApplication.projectChanged -= Reload; EditorSceneManagerHook(false); Clear(); if (show != null && show.model != null && clips.TryGetValue("Idle", out var idle)) idle.SampleAnimation(show.model.gameObject, 0f); }

        void Load()
        {
            show = FindAnyObjectByType<SidecarShowcase>();
            clips.Clear();
            if (show == null || show.model == null || show.manifest == null) { names = Array.Empty<string>(); return; }
            foreach (var c in AnimationUtility.GetAnimationClips(show.model.gameObject)) clips[c.name] = c;
            var list = show.ManifestClips().Where(c => clips.ContainsKey(c.name)).ToList();
            names = list.Select(c => c.name).ToArray();
            labels = list.Select(c => GroupOf(c.kind) + "/" + SidecarShowcase.LabelFor(c.name)).ToArray();
            chosen = Mathf.Clamp(chosen, 0, Math.Max(0, names.Length - 1));
            if (names.Length > 0) Choose(chosen);
        }

        // New model files replace the outfit and its moves under the window: look them up again rather than use the old ones.
        void Reload() { int keep = chosen; Load(); if (names.Length > 0) Choose(Mathf.Clamp(keep, 0, names.Length - 1)); Repaint(); }
        void EditorSceneManagerHook(bool on)
        {
            if (on) UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            else UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
        }
        void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode) => Reload();
        bool Stale => show == null || show.model == null || (names.Length > 0 && (!clips.TryGetValue(names[chosen], out var c) || c == null));

        static string GroupOf(string kind) => kind == "everyday" ? "Everyday" : kind == "riding" ? "Riding" : kind == "jump" ? "Jumps" : "Tricks";

        AnimationClip Clip => names.Length == 0 ? null : clips[names[chosen]];
        int Frames => Clip == null ? 0 : Mathf.RoundToInt(Clip.length * Fps);

        void Choose(int i)
        {
            chosen = i; frame = 0; Clear();
            var c = show.ManifestClips().First(x => x.name == names[i]);
            var m = show.model.transform;
            if (c.props != null)
                foreach (var p in c.props)
                {
                    var src = show.propModels == null ? null : Array.Find(show.propModels, x => x != null && x.name == p.prop);
                    if (src == null) src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DesktopRider/Sidecar/Props/" + p.prop + ".fbx");
                    if (src == null) continue;
                    var go = (GameObject)Instantiate(src, m.position + m.TransformDirection(show.ForwardLocal()) * p.x, m.rotation);
                    go.hideFlags = HideFlags.DontSave; props.Add(go);
                }
            ShowFrame();
        }

        void Clear() { foreach (var p in props) if (p != null) DestroyImmediate(p); props.Clear(); }

        void ShowFrame()
        {
            if (Stale) { Load(); if (Stale) return; }
            if (Clip == null) return;
            Clip.SampleAnimation(show.model.gameObject, Mathf.Min(frame / (float)Fps, Clip.length));
            SceneView.RepaintAll(); Repaint();
        }

        void Tick()
        {
            if (!playing || EditorApplication.isPlaying) return;
            if (Stale) { Load(); if (Stale) { playing = false; return; } }
            if (Clip == null) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - last < 1.0 / (Fps * speed)) return;
            last = now;
            frame++;
            if (frame > Frames) frame = 0;
            ShowFrame();
        }

        void OnGUI()
        {
            if (EditorApplication.isPlaying) { EditorGUILayout.HelpBox("Stop Play to use the Move Viewer (while playing, use the panel in the Game view).", MessageType.Info); return; }
            if (Stale) Load();
            if (names.Length == 0)
            {
                EditorGUILayout.HelpBox("Open the sidecar viewer scene first: Desktop Rider > Sidecar > Open the sidecar viewer.", MessageType.Info);
                if (GUILayout.Button("Look again")) Load();
                return;
            }
            EditorGUI.BeginChangeCheck();
            int pick = EditorGUILayout.Popup("Move", chosen, labels);
            if (EditorGUI.EndChangeCheck()) Choose(pick);
            EditorGUI.BeginChangeCheck();
            int f = EditorGUILayout.IntSlider("Frame", frame, 0, Frames);
            if (EditorGUI.EndChangeCheck()) { frame = f; playing = false; ShowFrame(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ Previous")) { playing = false; frame = Mathf.Max(0, frame - 1); ShowFrame(); }
                if (GUILayout.Button(playing ? "❚❚ Pause" : "▶ Play")) { playing = !playing; last = EditorApplication.timeSinceStartup; }
                if (GUILayout.Button("Next ▶")) { playing = false; frame = Mathf.Min(Frames, frame + 1); ShowFrame(); }
            }
            speed = EditorGUILayout.Slider("Speed", speed, 0.1f, 1.5f);
            var c = show.ManifestClips().First(x => x.name == names[chosen]);
            if (c.info != null && !string.IsNullOrEmpty(c.info.note)) EditorGUILayout.LabelField(c.info.note, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Watch it in the Scene view. Frame " + frame + " of " + Frames + (c.loop ? " (repeats)" : ""), EditorStyles.miniLabel);
        }
    }
}
