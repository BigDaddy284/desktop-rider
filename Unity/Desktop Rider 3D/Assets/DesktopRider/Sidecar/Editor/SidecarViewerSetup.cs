using System;
using System.IO;
using System.Linq;
using System.Text;
using DesktopRider.Sidecar;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesktopRider.Sidecar.EditorTools
{
    /// <summary>
    /// Desktop Rider > Sidecar > Open the sidecar viewer: opens a scene of its own with the sidecar outfit, the crew and the
    /// props; press Play and pick a move. Building the scene never touches Harvey's scene or models.
    /// Also answers Bridge/sidecar-command.txt ("id setup", "id check", "id shot Clip seconds") for automated checks.
    /// </summary>
    [InitializeOnLoad]
    public static class SidecarViewerSetup
    {
        const string Folder = SidecarImport.Folder;
        const string ScenePath = Folder + "SidecarViewer.unity";
        const string ModelPath = Folder + "Models/Sidecar41.fbx";
        const string ManifestPath = Folder + "Models/Sidecar41_manifest.json";
        static readonly string Bridge = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Bridge");
        static double next;

        static SidecarViewerSetup() { EditorApplication.update += Tick; }

        [MenuItem("Desktop Rider/Sidecar/Open the sidecar viewer")]
        static void OpenViewer()
        {
            if (!File.Exists(ScenePath)) Debug.Log(Build());
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("Sidecar viewer open: press Play, then pick a move from the panel on the left.");
        }

        [MenuItem("Desktop Rider/Sidecar/Rebuild the sidecar viewer")]
        static void MenuBuild() => Debug.Log(Build());

        /// <summary>Makes the viewer scene beside whatever is open (added, saved, then closed again).</summary>
        public static string Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (model == null || manifest == null) return "ERROR the sidecar model or its manifest is not imported yet";
            var active = SceneManager.GetActiveScene();
            bool openAlready = SceneManager.GetSceneByPath(ScenePath).isLoaded;
            if (openAlready) return "ERROR the viewer scene is open; close it or use it as it is";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(50f, 140f, 0f);
                var camGo = new GameObject("Viewer Camera"); var cam = camGo.AddComponent<Camera>(); camGo.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.62f, 0.72f, 0.82f); cam.fieldOfView = 35f; cam.nearClipPlane = 0.05f;
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Ground";
                ground.transform.localScale = new Vector3(40f, 1f, 40f);
                var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = new Color(0.45f, 0.55f, 0.32f) };
                if (groundMat.HasProperty("_BaseColor")) groundMat.SetColor("_BaseColor", new Color(0.45f, 0.55f, 0.32f));
                if (groundMat.HasProperty("_Smoothness")) groundMat.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(groundMat, Folder + "SidecarViewerGround.mat");
                ground.GetComponent<Renderer>().sharedMaterial = groundMat;
                var outfit = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                outfit.name = "Sidecar 41";
                var anim = outfit.GetComponent<UnityEngine.Animation>();
                if (anim == null) return "ERROR the sidecar model has no clips (it should import as legacy animation)";
                anim.playAutomatically = false;
                foreach (var r in outfit.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
                var show = new GameObject("Sidecar Viewer").AddComponent<SidecarShowcase>();
                show.model = anim; show.manifest = manifest; show.viewCamera = cam;
                show.propModels = AssetDatabase.FindAssets("t:GameObject", new[] { Folder + "Props" })
                    .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(g => g != null).ToArray();
                EditorSceneManager.SaveScene(scene, ScenePath);
                return "viewer scene saved to " + ScenePath + " with " + anim.GetClipCount() + " clips and " + show.propModels.Length + " props";
            }
            finally
            {
                if (active.IsValid()) SceneManager.SetActiveScene(active);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>What Unity made of the model: clips, how many curves each has, and the crew's blend shapes.</summary>
        public static string Check()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) return "ERROR no model at " + ModelPath;
            var sb = new StringBuilder();
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = r.sharedMesh;
                sb.AppendLine(r.name + ": " + m.vertexCount + " vertices, bones " + r.bones.Length + ", blend shapes " +
                              string.Join(",", Enumerable.Range(0, m.blendShapeCount).Select(m.GetBlendShapeName)) + ", material " + (r.sharedMaterial ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name + "/" + (r.sharedMaterial.mainTexture ? r.sharedMaterial.mainTexture.name : "no texture") : "none"));
            }
            foreach (var clip in AnimationUtility.GetAnimationClips(model))
            {
                var b = AnimationUtility.GetCurveBindings(clip);
                sb.AppendLine(string.Format("{0}: {1:0.00} s, {2}, curves {3}, blend shape curves {4}", clip.name, clip.length, clip.wrapMode, b.Length, b.Count(x => x.propertyName.StartsWith("blendShape."))));
            }
            return sb.ToString();
        }

        /// <summary>A picture of one moment of one clip (for checking the import without pressing Play).</summary>
        public static string Shot(string argument, string file)
        {
            var parts = argument.Split(' ');
            string name = parts[0]; float seconds = parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0f;
            float yaw = parts.Length > 2 ? float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) : 215f;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var preview = new PreviewRenderUtility();
            try
            {
                var go = (GameObject)UnityEngine.Object.Instantiate(model);
                preview.AddSingleGO(go);
                var clip = AnimationUtility.GetAnimationClips(model).FirstOrDefault(c => c.name == name);
                if (clip == null) return "ERROR no clip " + name;
                var chassis = go.GetComponentsInChildren<Transform>().First(t => t.name == "chassis");
                // "side": a wide side-on view with the move's ramps placed as the viewer places them, to check where it lands
                bool side = parts.Length > 3 && parts[3] == "side";
                if (side)
                {
                    var root = go.GetComponentsInChildren<Transform>().First(t => t.name == "root" && t.parent != null && t.parent.name == "sidecar_rig");
                    clip.SampleAnimation(go, 0f); var a0 = go.transform.InverseTransformPoint(root.position);
                    clip.SampleAnimation(go, clip.length); var a1 = go.transform.InverseTransformPoint(root.position);
                    var fwd = go.transform.TransformDirection((a1 - a0).normalized);
                    var manifest = JsonUtility.FromJson<SidecarShowcase.Manifest>(AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath).text);
                    var info = manifest.clips.FirstOrDefault(c => c.name == name);
                    if (info != null && info.props != null)
                        foreach (var p in info.props)
                        {
                            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Props/" + p.prop + ".fbx");
                            if (src == null) continue;
                            var prop = (GameObject)UnityEngine.Object.Instantiate(src, go.transform.position + fwd * p.x, go.transform.rotation);
                            preview.AddSingleGO(prop);
                        }
                }
                clip.SampleAnimation(go, Mathf.Min(seconds, clip.length));
                var target = side ? chassis.position : chassis.position + Vector3.up * 0.6f;
                var rot = Quaternion.Euler(side ? 0f : 22f, yaw, 0f);
                preview.camera.transform.SetPositionAndRotation(target - rot * Vector3.forward * (side ? 14f : 5.5f), rot);
                preview.camera.fieldOfView = 35f; preview.camera.nearClipPlane = 0.05f; preview.camera.farClipPlane = 100f;
                preview.camera.clearFlags = CameraClearFlags.SolidColor; preview.camera.backgroundColor = new Color(0.62f, 0.72f, 0.82f);
                preview.lights[0].intensity = 1.3f; preview.lights[0].transform.rotation = Quaternion.Euler(50f, 140f, 0f);
                preview.ambientColor = new Color(0.5f, 0.5f, 0.55f);
                preview.BeginStaticPreview(new Rect(0, 0, 900, 700));
                preview.Render(true);
                var tex = preview.EndStaticPreview();
                File.WriteAllBytes(file, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                var sb = new StringBuilder("saved " + file);
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>()) sb.Append("\n" + r.name + " bounds " + r.bounds.center + " size " + r.bounds.size + " root " + (r.rootBone ? r.rootBone.name + " " + r.rootBone.position : "none") + " enabled " + r.enabled);
                foreach (var t in go.GetComponentsInChildren<Transform>()) if (t.name.EndsWith("_rig") || t.name == "hips") sb.Append("\n" + t.name + " at " + t.position + " scale " + t.lossyScale);
                return sb.ToString();
            }
            finally { preview.Cleanup(); }
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 1.0;
            string file = Path.Combine(Bridge, "sidecar-command.txt");
            if (!File.Exists(file) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string[] parts;
            try { parts = File.ReadAllText(file).Trim().Split(new[] { ' ' }, 3); File.Delete(file); }
            catch (IOException) { return; }
            if (parts.Length < 2) return;
            string answer;
            try
            {
                switch (parts[1])
                {
                    case "setup": answer = Build(); break;
                    case "check": answer = Check(); break;
                    case "shot": answer = Shot(parts.Length > 2 ? parts[2] : "Idle", Path.Combine(Bridge, "sidecar-shot-" + parts[0] + ".png")); break;
                    case "refresh": AssetDatabase.Refresh(); answer = "refreshed"; break;
                    case "open":
                        if (SceneManager.GetActiveScene().isDirty) { answer = "ERROR the open scene has unsaved changes; not switching"; break; }
                        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); answer = "opened " + ScenePath; break;
                    case "playclip":
                    {
                        var show = UnityEngine.Object.FindFirstObjectByType<SidecarShowcase>();
                        if (!EditorApplication.isPlaying || show == null) { answer = "ERROR not playing the viewer"; break; }
                        string arg = parts.Length > 2 ? parts[2] : "Idle";
                        if (arg == "RideRound") show.StartRideRound(); else show.Play(arg);
                        answer = "playing " + arg; break;
                    }
                    case "camshot":
                    {
                        var show = UnityEngine.Object.FindFirstObjectByType<SidecarShowcase>();
                        if (!EditorApplication.isPlaying || show == null || show.viewCamera == null) { answer = "ERROR not playing the viewer"; break; }
                        var rt = new RenderTexture(900, 700, 24); var cam = show.viewCamera; var old = cam.targetTexture;
                        cam.targetTexture = rt; cam.Render(); cam.targetTexture = old;
                        RenderTexture.active = rt; var tex = new Texture2D(900, 700, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 900, 700), 0, 0); tex.Apply(); RenderTexture.active = null;
                        string f = Path.Combine(Bridge, "sidecar-cam-" + parts[0] + ".png"); File.WriteAllBytes(f, tex.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(tex); rt.Release();
                        answer = "saved " + f + " now showing " + show.Now + "\n" + show.Report(); break;
                    }
                    default: answer = "ERROR unknown command " + parts[1]; break;
                }
            }
            catch (Exception e) { answer = "ERROR " + e; }
            File.WriteAllText(Path.Combine(Bridge, "sidecar-result-" + parts[0] + ".txt"), answer);
        }
    }
}
