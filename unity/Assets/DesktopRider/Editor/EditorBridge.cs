using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DesktopRider.Overlay;
using DesktopRider.Riding;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace DesktopRider.EditorTools
{
    /// <summary>
    /// Lets the build be driven without clicking in the editor. Put a line "id command arguments" in Bridge/command.txt
    /// at the top of the project; the answer appears in Bridge/result-id.txt. Also available by hand under the Desktop Rider menu.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorBridge
    {
        const string ModelPath = "Assets/DesktopRider/Models/DesktopRider.fbx";            // textured model from Blender (kept off GitHub)
        const string ProofModelPath = "Assets/DesktopRider/Models/Proof/RiderProof.fbx";   // rough stand-in if the textured one is missing
        const string ArtFolder = "Assets/DesktopRider/Art/Proof";
        static readonly string Root = Directory.GetParent(Application.dataPath).FullName;
        static readonly string Folder = Path.Combine(Root, "Bridge");
        static readonly string BuildFolder = Path.Combine(Root, "Builds", "Proof");
        static readonly string Exe = Path.Combine(BuildFolder, "DesktopRider3D.exe");
        static double nextCheck;

        static EditorBridge()
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "loaded.txt"), DateTime.Now.ToString("HH:mm:ss") + " Unity " + Application.unityVersion + "\n");
            CompilationPipeline.assemblyCompilationFinished += OnCompiled;
            EditorApplication.update += Tick;
        }

        static void OnCompiled(string assembly, CompilerMessage[] messages)
        {
            var errors = messages.Where(m => m.type == CompilerMessageType.Error).Select(m => m.file + "(" + m.line + "): " + m.message).ToArray();
            File.AppendAllText(Path.Combine(Folder, "compile.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + Path.GetFileName(assembly) + " errors=" + errors.Length + "\n" + string.Join("\n", errors) + (errors.Length > 0 ? "\n" : ""));
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1.0;
            string file = Path.Combine(Folder, "command.txt");
            if (!File.Exists(file) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            string[] parts;
            try { parts = File.ReadAllText(file).Trim().Split(new[] { ' ' }, 3); File.Delete(file); }
            catch (IOException) { return; }
            if (parts.Length < 2) return;
            string id = parts[0], command = parts[1], argument = parts.Length > 2 ? parts[2] : "";

            string answer;
            try { answer = "OK\n" + Run(id, command, argument); }
            catch (Exception e) { answer = "ERROR\n" + e; }
            if (answer != "OK\n" + Deferred) File.WriteAllText(Path.Combine(Folder, "result-" + id + ".txt"), answer);
        }

        const string Deferred = "<deferred>";

        static string Run(string id, string command, string argument)
        {
            switch (command)
            {
                case "ping": return "pong " + Application.unityVersion + " scene=" + EditorSceneManager.GetActiveScene().path;
                case "refresh": File.WriteAllText(Path.Combine(Folder, "result-" + id + ".txt"), "OK\nrefresh started"); AssetDatabase.Refresh(); return Deferred;
                case "addpackage": UnityEditor.PackageManager.Client.Add(argument); return "requested " + argument;
                case "setup": return SetUp();
                case "build": return Build();
                case "run": return Launch(argument);
                case "stop": File.WriteAllText(Path.Combine(BuildFolder, "quit.txt"), "quit"); return "asked the rider to close";
                case "test": return RunTests(id);
                case "snapshot": return Snapshot(argument);
                default: throw new ArgumentException("Unknown command " + command);
            }
        }

        [MenuItem("Desktop Rider/Proof/1 Set up scene and player settings")] static void MenuSetUp() => Debug.Log(SetUp());
        [MenuItem("Desktop Rider/Proof/2 Build")] static void MenuBuild() => Debug.Log(Build());
        [MenuItem("Desktop Rider/Proof/3 Run")] static void MenuRun() => Debug.Log(Launch(""));
        [MenuItem("Desktop Rider/Proof/4 Stop")] static void MenuStop() => File.WriteAllText(Path.Combine(BuildFolder, "quit.txt"), "quit");

        // ------------------------------------------------------------------ set-up
        static string SetUp()
        {
            var log = new StringBuilder();

            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.forceSingleInstance = true;
            PlayerSettings.useFlipModelSwapchain = false;          // the flip model cannot show a see-through window
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            try { PlayerSettings.SplashScreen.show = false; } catch (Exception e) { log.AppendLine("splash: " + e.Message); }
            log.AppendLine("player settings applied");

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.supportsHDR = false;
                urp.msaaSampleCount = 2;
                EditorUtility.SetDirty(urp);
                log.AppendLine("render settings: " + urp.name + " HDR off, MSAA 2");
            }

            var camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null) throw new InvalidOperationException("The open scene has no camera.");
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            camera.transform.position = camera.transform.rotation * new Vector3(0f, 0f, -60f);
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            EditorUtility.SetDirty(camera);

            // The bike carries its own soft shadow, so the light's real-time shadow map (redrawn every frame) is not needed.
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                light.shadows = LightShadows.None;
                EditorUtility.SetDirty(light);
            }
            log.AppendLine("light shadows off");

            var volume = GameObject.Find("Global Volume");
            if (volume != null) volume.SetActive(false);

            foreach (string name in new[] { "Rider", "Overlay" })
            {
                var old = GameObject.Find(name);
                if (old != null) UnityEngine.Object.DestroyImmediate(old);
            }

            var overlay = new GameObject("Overlay");
            overlay.AddComponent<OverlayWindow>();
            overlay.AddComponent<ProofStats>();

            var rider = new GameObject("Rider");
            var box = rider.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.65f, 0f);
            box.size = new Vector3(0.6f, 1.4f, 1.9f);
            var lean = new GameObject("Lean");
            lean.transform.SetParent(rider.transform, false);

            var free = rider.AddComponent<FreeRider>();
            free.viewCamera = camera;
            free.leanPivot = lean.transform;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null)
            {
                log.AppendLine("textured model missing at " + ModelPath + ", using the proof model");
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProofModelPath);
            }
            if (prefab == null) log.AppendLine("MODEL MISSING at " + ModelPath + " and " + ProofModelPath);
            else
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                model.name = "Model";
                model.transform.SetParent(lean.transform, false);
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);    // Blender +X (the bike's nose) becomes Unity -X; turn it to face +Z
                free.modelAnimation = model.GetComponent<Animation>();
                var clips = AnimationUtility.GetAnimationClips(model).Select(c => c.name).ToArray();
                // The bike and rider rigs are separate roots under this one object; moving the object moves both.
                log.AppendLine("model placed: " + AssetDatabase.GetAssetPath(prefab) + "; clips: " + string.Join(", ", clips));
                var bounds = new Bounds(model.transform.position, Vector3.zero);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                log.AppendLine("model bounds centre " + bounds.center + " size " + bounds.size);
            }

            rider.transform.position = Vector3.zero;
            CreateShadow(rider.transform, log);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            log.AppendLine("scene saved: " + scene.path);
            return log.ToString();
        }

        /// <summary>
        /// Draws the rider to Bridge/snapshot-yaw.png so the model can be checked without looking at the screen.
        /// Argument: "yaw [clip] [seconds]", e.g. "45 Idle 0.5". The scene is not saved afterwards.
        /// </summary>
        static string Snapshot(string argument)
        {
            var parts = (argument ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            float yaw = parts.Length > 0 ? float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) : 45f;
            var model = GameObject.Find("Rider/Lean/Model");
            if (model == null) return "ERROR no Rider/Lean/Model in the scene; send setup first";
            if (parts.Length > 1)
            {
                var clip = AnimationUtility.GetAnimationClips(model).FirstOrDefault(c => c.name == parts[1]);
                if (clip == null) return "ERROR no clip " + parts[1];
                float t = parts.Length > 2 ? float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) : 0f;
                clip.SampleAnimation(model, t);
            }
            var go = new GameObject("SnapshotCamera");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 1.1f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.6f, 0.65f, 1f);
                var target = new Vector3(0f, 0.75f, 0f);
                go.transform.rotation = Quaternion.Euler(30f, yaw, 0f);
                go.transform.position = target - go.transform.forward * 10f;
                var rt = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var image = new Texture2D(768, 768, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                image.Apply();
                RenderTexture.active = null;
                string path = Path.Combine(Folder, "snapshot-" + Mathf.RoundToInt(yaw) + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                cam.targetTexture = null;
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(image);
                var renderers = model.GetComponentsInChildren<Renderer>();
                var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().ToArray();
                int textured = materials.Count(m => m.mainTexture != null);
                return "saved " + path + "\nmaterials " + materials.Length + ", with a texture " + textured +
                       "\nshaders " + string.Join(", ", materials.Select(m => m.shader.name).Distinct()) +
                       "\ntextures " + string.Join(", ", materials.Select(m => m.mainTexture ? m.mainTexture.name + " " + m.mainTexture.width : null).Where(n => n != null).Distinct());
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void CreateShadow(Transform parent, StringBuilder log)
        {
            Directory.CreateDirectory(Path.Combine(Root, ArtFolder));
            string texturePath = ArtFolder + "/BlobShadow.png";
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy)));
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, a * 0.45f));
                }
            File.WriteAllBytes(Path.Combine(Root, texturePath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);

            string materialPath = ArtFolder + "/BlobShadow.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            EditorUtility.SetDirty(material);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Shadow";
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(1.3f, 2.5f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            log.AppendLine("shadow added");
        }

        // ------------------------------------------------------------------ build and run
        static string Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene.path },
                locationPathName = Exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            var text = new StringBuilder();
            text.AppendLine("result=" + s.result + " errors=" + s.totalErrors + " warnings=" + s.totalWarnings + " sizeMB=" + (s.totalSize / 1048576.0).ToString("F1") + " seconds=" + s.totalTime.TotalSeconds.ToString("F0"));
            text.AppendLine("output=" + s.outputPath);
            foreach (var step in report.steps)
                foreach (var message in step.messages)
                    if (message.type == LogType.Error || message.type == LogType.Exception) text.AppendLine(message.content);
            return text.ToString();
        }

        static string Launch(string arguments)
        {
            if (!File.Exists(Exe)) throw new FileNotFoundException("Build first.", Exe);
            string quit = Path.Combine(BuildFolder, "quit.txt");
            if (File.Exists(quit)) File.Delete(quit);
            var process = Process.Start(new ProcessStartInfo(Exe, arguments) { WorkingDirectory = BuildFolder, UseShellExecute = false });
            return "started process " + process.Id;
        }

        // ------------------------------------------------------------------ tests
#if DR_HAS_TEST_FRAMEWORK
        sealed class Callbacks : UnityEditor.TestTools.TestRunner.Api.ICallbacks
        {
            public string File;
            readonly StringBuilder failures = new StringBuilder();
            public void RunStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor tests) { }
            public void TestStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor test) { }
            public void TestFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.TestStatus == UnityEditor.TestTools.TestRunner.Api.TestStatus.Failed)
                    failures.AppendLine("FAILED " + result.FullName + ": " + result.Message);
            }
            public void RunFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result)
            {
                System.IO.File.WriteAllText(File, "OK\npassed=" + result.PassCount + " failed=" + result.FailCount + " skipped=" + result.SkipCount + " inconclusive=" + result.InconclusiveCount + "\n" + failures);
            }
        }

        static string RunTests(string id)
        {
            var api = ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks { File = Path.Combine(Folder, "result-" + id + ".txt") });
            api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(new UnityEditor.TestTools.TestRunner.Api.Filter { testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode }));
            return Deferred;
        }
#else
        static string RunTests(string id) => throw new InvalidOperationException("The Unity test framework package is not installed. Send: addpackage com.unity.test-framework");
#endif
    }
}
