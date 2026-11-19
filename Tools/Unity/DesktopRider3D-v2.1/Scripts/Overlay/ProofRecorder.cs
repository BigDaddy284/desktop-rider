using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DesktopRider.Riding;
using UnityEngine;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Checks only (runs started with --proof). When a file called record.txt appears beside the program (holding a number
    /// of seconds, 20 if empty), saves what the rider's own window draws, frame by frame, into a record-... folder with a
    /// line per frame in frames.csv: where the window is, the size Unity drew at, the rider and the clips playing. Only the
    /// program's own picture is saved, never the rest of the desktop.
    /// </summary>
    public sealed class ProofRecorder : MonoBehaviour
    {
        public int maxWidth = 480;

        static readonly bool ForChecks = Array.IndexOf(Environment.GetCommandLineArgs(), "--proof") >= 0;
        string folder;
        float nextCheck;
        bool recording;

        void Start()
        {
            if (!ForChecks || Application.isEditor) { enabled = false; return; }
            folder = Path.GetDirectoryName(Application.dataPath);
        }

        void Update()
        {
            if (recording || Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + 1f;
            string file = Path.Combine(folder, "record.txt");
            if (!File.Exists(file)) return;
            float seconds = 20f;
            try
            {
                float.TryParse(File.ReadAllText(file).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
                File.Delete(file);
            }
            catch (IOException) { return; }
            if (seconds <= 0f) seconds = 20f;
            StartCoroutine(Record(Mathf.Min(seconds, 60f)));
        }

        IEnumerator Record(float seconds)
        {
            recording = true;
            string outDir = Path.Combine(folder, "record-" + DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(outDir);
            var csv = new StringBuilder("frame,time,winX,winY,winW,winH,drawnW,drawnH,riderX,riderY,heading,speed,inTrick,approaching,clip,framing,orthoSize,states\n");
            var rider = FindFirstObjectByType<FreeRider>();
            var camera = Camera.main;
            float end = Time.unscaledTime + seconds;
            int n = 0;
            Debug.Log("Recording " + seconds + " s into " + outDir);
            while (Time.unscaledTime < end)
            {
                yield return new WaitForEndOfFrame();
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                int w = shot.width, h = shot.height;
                float scale = Mathf.Min(1f, (float)maxWidth / Mathf.Max(1, w));
                int sw = Mathf.Max(1, Mathf.RoundToInt(w * scale)), sh = Mathf.Max(1, Mathf.RoundToInt(h * scale));
                var rt = RenderTexture.GetTemporary(sw, sh, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(shot, rt);
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var small = new Texture2D(sw, sh, TextureFormat.RGBA32, false);
                small.ReadPixels(new Rect(0, 0, sw, sh), 0, 0);
                small.Apply();
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                File.WriteAllBytes(Path.Combine(outDir, n.ToString("D4") + ".png"), small.EncodeToPNG());
                Destroy(small);
                Destroy(shot);

                var win = OverlayWindow.WindowRect;
                string riderPart = ",,,,,,,";
                string states = "";
                if (rider != null)
                {
                    var p = rider.DesktopPosition;
                    var f = rider.Framing;
                    riderPart = string.Format(CultureInfo.InvariantCulture, "{0:F1},{1:F1},{2:F1},{3:F2},{4},{5},{6},{7}",
                        p.x, p.y, rider.transform.eulerAngles.y, rider.Speed, rider.InTrick, rider.Approaching, rider.Director.Clip,
                        f is Rect r ? string.Format(CultureInfo.InvariantCulture, "{0:F0} {1:F0} {2:F0} {3:F0}", r.x, r.y, r.width, r.height) : "");
                    if (rider.modelAnimation != null)
                        states = string.Join(" ", rider.modelAnimation.Cast<AnimationState>().Where(s => s.enabled && s.weight > 0.001f)
                            .Select(s => string.Format(CultureInfo.InvariantCulture, "{0}:{1:F2}@{2:F2}", s.name, s.weight, s.time)));
                }
                csv.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:F3},{2},{3},{4},{5},{6},{7},{8},{9:F3},{10}\n",
                    n, Time.unscaledTime, win.x, win.y, win.width, win.height, w, h, riderPart, camera != null ? camera.orthographicSize : 0f, states);
                n++;
            }
            File.WriteAllText(Path.Combine(outDir, "frames.csv"), csv.ToString());
            Debug.Log("Recorded " + n + " frames into " + outDir);
            recording = false;
        }
    }
}
