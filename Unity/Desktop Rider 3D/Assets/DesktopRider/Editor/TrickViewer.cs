using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesktopRider.Riding;
using DesktopRider.Tricks;
using UnityEditor;
using UnityEngine;

namespace DesktopRider.EditorTools
{
    /// <summary>
    /// Desktop Rider > Trick Viewer: step through any trick frame by frame and see exactly what the app's window shows,
    /// with the ramp and landing in place. The grey area is the window the app opens for that trick; the readout says
    /// whether the bike and rider stay inside it at this frame. "Save every frame" writes the frames as pictures.
    /// Works with the editor stopped; it moves the rider in the scene while open and puts it back when closed.
    /// </summary>
    public sealed class TrickViewer : EditorWindow
    {
        const float PixelsPerMetre = 100f;      // the app at 100% screen scaling and normal rider size
        const float Pitch = 45f;

        TrickLayout layout;
        TrickRunner runner;
        FreeRider rider;
        GameObject model;
        Transform reference;
        readonly Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        readonly List<GameObject> shownProps = new List<GameObject>();
        string[] names = Array.Empty<string>();
        int chosen, frame;
        TrickExtent extent;
        Rect frameRect;
        RenderTexture picture;
        string readout = "";
        bool outside;
        Vector3 savedPosition;
        Quaternion savedRotation;
        bool moved;

        [MenuItem("Desktop Rider/Trick Viewer")]
        static void Open() => GetWindow<TrickViewer>("Trick Viewer").Show();

        void OnEnable() => Load();

        void OnDisable() => Restore();

        void Load()
        {
            clips.Clear();
            runner = FindFirstObjectByType<TrickRunner>(FindObjectsInactive.Include);
            rider = runner != null ? runner.rider : null;
            var lean = rider != null ? rider.transform.Find("Lean") : null;
            model = lean != null ? lean.Find("Model")?.gameObject : null;
            reference = rider != null && rider.leanPivot != null ? rider.leanPivot : rider != null ? rider.transform : null;
            if (runner == null || runner.layoutFile == null || model == null) { names = Array.Empty<string>(); return; }
            layout = TrickLayout.Parse(runner.layoutFile.text);
            if (runner.tricksModel != null)
                foreach (var clip in AnimationUtility.GetAnimationClips(runner.tricksModel.gameObject)) clips[clip.name] = clip;
            names = layout.TrickNames.Where(clips.ContainsKey).ToArray();
            chosen = Mathf.Clamp(chosen, 0, Math.Max(0, names.Length - 1));
            if (names.Length > 0) Choose(chosen);
        }

        TrickAnimation Trick => names.Length == 0 ? null : layout.Find(names[chosen]);

        void Choose(int index)
        {
            chosen = index;
            frame = 0;
            var trick = Trick;
            if (trick == null) return;
            if (!moved)
            {
                savedPosition = rider.transform.position; savedRotation = rider.transform.rotation; moved = true;
            }
            rider.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            extent = TrickExtents.Measure(model, clips[trick.Name], reference, trick.TravelAt);
            var view = View();
            frameRect = TrickRunner.FrameFor(view, extent, trick, Vector3.zero, 0f, TrickRunner.HalfWidth(layout, trick));
            PlaceProps(trick);
            ShowFrame();
        }

        static DesktopView View() => new DesktopView(4000f, 4000f, PixelsPerMetre, Pitch);

        void PlaceProps(TrickAnimation trick)
        {
            ClearProps();
            var run = new TrickRun(trick, System.Numerics.Vector2.Zero, 0f);
            foreach (var placed in run.Props)
            {
                var source = runner.propModels.FirstOrDefault(p => p != null && p.name == placed.Name);
                if (source == null) continue;
                var prop = (GameObject)Instantiate(source);
                prop.hideFlags = HideFlags.DontSave;
                prop.transform.SetPositionAndRotation(new Vector3(placed.Position.X, 0f, placed.Position.Y), Quaternion.Euler(0f, placed.Heading + runner.propYaw, 0f));
                shownProps.Add(prop);
            }
        }

        void ClearProps()
        {
            foreach (var p in shownProps) if (p != null) DestroyImmediate(p);
            shownProps.Clear();
        }

        void Restore()
        {
            ClearProps();
            if (moved && rider != null)
            {
                rider.transform.SetPositionAndRotation(savedPosition, savedRotation);
                if (model != null)
                {
                    var idle = AnimationUtility.GetAnimationClips(model).FirstOrDefault(c => c.name == "Idle");
                    if (idle != null) idle.SampleAnimation(model, 0f);
                }
            }
            moved = false;
            if (picture != null) { picture.Release(); DestroyImmediate(picture); picture = null; }
        }

        int FrameCount => Trick == null ? 0 : Trick.Frames;

        void ShowFrame()
        {
            var trick = Trick;
            if (trick == null) return;
            float seconds = (float)(frame / trick.FramesPerSecond);
            var clip = clips[trick.Name];
            clip.SampleAnimation(model, Mathf.Min(seconds, clip.length));
            float along = (float)(trick.TravelAt(seconds) - trick.TravelAt(0));
            rider.transform.position = Vector3.forward * along;

            // Is every bone (with room for the wheels and helmet) inside the window?
            var view = View();
            outside = false;
            foreach (var bone in model.GetComponentsInChildren<Transform>())
            {
                foreach (var offset in new[] { Vector3.zero, Vector3.up * TrickExtents.BoneMargin, Vector3.forward * TrickExtents.BoneMargin, Vector3.back * TrickExtents.BoneMargin })
                {
                    var px = view.ToDesktop(bone.position + offset);
                    if (!TrickWindow().Contains(px)) outside = true;
                }
            }
            readout = string.Format("{0}: frame {1} of {2} ({3:0.00} s). {4}", trick.Name, frame, trick.Frames, seconds,
                outside ? "PART OF THE BIKE OR RIDER IS OUTSIDE THE WINDOW" : "Everything is inside the window.");
            Render(view);
            Repaint();
        }

        /// <summary>The window the app opens for the trick (the same sum as the app, on a desktop big enough for it).</summary>
        Rect TrickWindow()
        {
            var r = FreeRider.TrickWindow(frameRect, 4000, 4000);
            return new Rect(r.x, r.y, r.width, r.height);
        }

        void Render(DesktopView view)
        {
            var window = TrickWindow();
            float shrink = Mathf.Min(1f, 1024f / Mathf.Max(window.width, window.height));
            int width = Mathf.Max(16, Mathf.RoundToInt(window.width * shrink)), height = Mathf.Max(16, Mathf.RoundToInt(window.height * shrink));
            if (picture == null || picture.width != width || picture.height != height)
            {
                if (picture != null) { picture.Release(); DestroyImmediate(picture); }
                picture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            }
            var go = new GameObject("TrickViewerCamera") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f, 1f);
                cam.orthographicSize = window.height / (2f * PixelsPerMetre);
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 200f;
                go.transform.SetPositionAndRotation(view.CameraFor(window.center), view.Rotation);
                cam.targetTexture = picture;
                cam.Render();
                cam.targetTexture = null;
            }
            finally { DestroyImmediate(go); }
        }

        void OnGUI()
        {
            if (names.Length == 0)
            {
                EditorGUILayout.HelpBox("No tricks found. Run Desktop Rider > Proof > 1 Set Up Scene (or the bridge's setup) with the trick and prop models imported, then reopen this window.", MessageType.Info);
                if (GUILayout.Button("Look again")) Load();
                return;
            }
            EditorGUI.BeginChangeCheck();
            int pick = EditorGUILayout.Popup("Trick", chosen, names);
            if (EditorGUI.EndChangeCheck()) Choose(pick);

            EditorGUI.BeginChangeCheck();
            int f = EditorGUILayout.IntSlider("Frame", frame, 0, FrameCount);
            if (EditorGUI.EndChangeCheck()) { frame = f; ShowFrame(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀ Previous")) { frame = Mathf.Max(0, frame - 1); ShowFrame(); }
                if (GUILayout.Button("Next ▶")) { frame = Mathf.Min(FrameCount, frame + 1); ShowFrame(); }
                if (GUILayout.Button("Save every frame")) SaveAll();
            }
            var style = new GUIStyle(EditorStyles.wordWrappedLabel) { fontStyle = outside ? FontStyle.Bold : FontStyle.Normal };
            style.normal.textColor = outside ? new Color(0.9f, 0.2f, 0.2f) : style.normal.textColor;
            GUILayout.Label(readout, style);
            GUILayout.Label("Room this trick takes: " + extent, EditorStyles.miniLabel);
            if (picture != null)
            {
                float fit = Mathf.Min((position.width - 16f) / picture.width, (position.height - 110f) / picture.height);
                var r = GUILayoutUtility.GetRect(picture.width * fit, picture.height * fit, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(r, picture, ScaleMode.ScaleToFit, false);
            }
        }

        void SaveAll() => Debug.Log(SaveCurrent());

        string SaveCurrent()
        {
            var trick = Trick;
            if (trick == null) return "no trick chosen";
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Bridge", "trick-frames", trick.Name);
            Directory.CreateDirectory(folder);
            int outsideFrames = 0;
            for (frame = 0; frame <= FrameCount; frame++)
            {
                ShowFrame();
                if (outside) outsideFrames++;
                RenderTexture.active = picture;
                var image = new Texture2D(picture.width, picture.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, picture.width, picture.height), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(folder, frame.ToString("0000") + ".png"), image.EncodeToPNG());
                DestroyImmediate(image);
            }
            frame = 0; ShowFrame();
            return "Trick Viewer saved " + (FrameCount + 1) + " frames of " + trick.Name + " to " + folder + "; frames with something outside the window: " + outsideFrames;
        }

        /// <summary>For the editor bridge: saves every frame of the named trick (or of every trick with "all").</summary>
        public static string SaveFrames(string name)
        {
            var viewer = CreateInstance<TrickViewer>();
            try
            {
                if (viewer.names.Length == 0) viewer.Load();
                if (viewer.names.Length == 0) return "ERROR no tricks found; send setup first";
                var picked = string.IsNullOrWhiteSpace(name) || name.Trim() == "all" ? viewer.names : new[] { name.Trim() };
                var lines = new List<string>();
                foreach (var n in picked)
                {
                    int i = Array.IndexOf(viewer.names, n);
                    if (i < 0) { lines.Add("no trick called " + n + "; tricks: " + string.Join(", ", viewer.names)); continue; }
                    viewer.Choose(i);
                    lines.Add(viewer.SaveCurrent());
                }
                return string.Join("\n", lines);
            }
            finally
            {
                viewer.Restore();
                DestroyImmediate(viewer);
            }
        }

        /// <summary>For the editor bridge: checks every trick and every frame; one line per trick.</summary>
        public static string CheckAll()
        {
            var viewer = CreateInstance<TrickViewer>();
            try
            {
                if (viewer.names.Length == 0) viewer.Load();
                if (viewer.names.Length == 0) return "ERROR no tricks found; send setup first";
                var lines = new List<string>();
                for (int i = 0; i < viewer.names.Length; i++)
                {
                    viewer.Choose(i);
                    int bad = 0;
                    for (viewer.frame = 0; viewer.frame <= viewer.FrameCount; viewer.frame++)
                    {
                        viewer.ShowFrame();
                        if (viewer.outside) bad++;
                    }
                    var w = viewer.TrickWindow();
                    lines.Add(string.Format("{0}: window {4:0}x{1:0} px, {2}; frames outside: {3}", viewer.names[i], w.height, viewer.extent, bad, w.width));
                }
                return string.Join("\n", lines);
            }
            finally
            {
                viewer.Restore();
                DestroyImmediate(viewer);
            }
        }
    }
}
