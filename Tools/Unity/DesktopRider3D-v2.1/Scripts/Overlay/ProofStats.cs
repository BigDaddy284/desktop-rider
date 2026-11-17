using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Checks only (runs started with --proof). Phase 1 measurement: every few seconds writes frame rate, memory and processor use to proof-stats.txt
    /// beside the program. Also closes the program when a file called quit.txt appears there, or on Ctrl+Shift+Q, and starts a
    /// trick when trick.txt appears (for checks from the editor bridge).
    /// </summary>
    public sealed class ProofStats : MonoBehaviour
    {
        public float interval = 5f;

        string folder;
        float nextReport;
        int frames;
        float windowStart;
        TimeSpan lastCpu;
        DateTime lastWall;
        Process process;

        /// <summary>
        /// Only for runs started from the editor bridge (which passes --proof): a copy started any other way has no stats
        /// file, no test files and no Ctrl+Shift+Q, so the program people use carries none of the checking hooks.
        /// </summary>
        static readonly bool ForChecks = Array.IndexOf(Environment.GetCommandLineArgs(), "--proof") >= 0;

        void Start()
        {
            if (!ForChecks && !Application.isEditor) { enabled = false; return; }
            folder = Path.GetDirectoryName(Application.dataPath);
            process = Process.GetCurrentProcess();
            lastCpu = process.TotalProcessorTime;
            lastWall = DateTime.UtcNow;
            windowStart = Time.unscaledTime;
            nextReport = Time.unscaledTime + interval;
            if (!Application.isEditor) File.WriteAllText(Path.Combine(folder, "proof-stats.txt"), "time,fps,targetFps,memoryMB,cpuPercentOfOneCore,overlayActive,graphics\n");
        }

        void Update()
        {
            frames++;
            if (Time.unscaledTime < nextReport) return;
            nextReport = Time.unscaledTime + interval;
            if (Application.isEditor) return;

            if (File.Exists(Path.Combine(folder, "quit.txt"))) { Application.Quit(); return; }
            UseTestCharacter();
            StartTestTrick();

            process.Refresh();
            var cpu = process.TotalProcessorTime;
            var wall = DateTime.UtcNow;
            double cpuPercent = (cpu - lastCpu).TotalSeconds / Math.Max(0.001, (wall - lastWall).TotalSeconds) * 100.0;
            lastCpu = cpu; lastWall = wall;
            float fps = frames / Mathf.Max(0.001f, Time.unscaledTime - windowStart);
            frames = 0; windowStart = Time.unscaledTime;

            string line = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:HH:mm:ss},{1:F1},{2},{3:F0},{4:F1},{5},{6}\n",
                DateTime.Now, fps, Application.targetFrameRate, WorkingSetBytes() / 1048576.0, cpuPercent, OverlayWindow.IsActive, SystemInfo.graphicsDeviceType);
            try { File.AppendAllText(Path.Combine(folder, "proof-stats.txt"), line); } catch (IOException) { }
        }

        /// <summary>
        /// For checks from the editor bridge: a file called character.txt beside the program ("Harvey" or "Sidecar41") swaps
        /// who rides for this run, without changing the saved settings.
        /// </summary>
        void UseTestCharacter()
        {
            string file = Path.Combine(folder, "character.txt");
            if (!File.Exists(file)) return;
            try
            {
                string name = File.ReadAllText(file).Trim();
                File.Delete(file);
                var list = FindFirstObjectByType<DesktopRider.Riding.RiderCharacters>();
                if (list != null && Enum.TryParse(name, out DesktopRider.Core.Configuration.RiderCharacter id)) list.Use(id);
                UnityEngine.Debug.Log("Test character " + name + (list != null ? ": riding " + list.Current : ": no characters in the scene"));
            }
            catch (IOException) { }
        }

        /// <summary>For checks from the editor bridge: a file called trick.txt beside the program starts the trick named in it.</summary>
        void StartTestTrick()
        {
            string file = Path.Combine(folder, "trick.txt");
            if (!File.Exists(file)) return;
            try
            {
                string name = File.ReadAllText(file).Trim();
                File.Delete(file);
                var runner = FindFirstObjectByType<DesktopRider.Riding.TrickRunner>();
                bool started = runner != null && runner.TryPerform(name.Length == 0 ? null : name);
                UnityEngine.Debug.Log("Test trick " + (name.Length == 0 ? "(random)" : name) + (started ? " started." : " did not start."));
            }
            catch (IOException) { }
        }

        // Mono's Process.WorkingSet64 reads 0 on Windows, so ask Windows directly.
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct MemoryCounters
        {
            public uint cb, PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
                QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("psapi.dll")] static extern bool GetProcessMemoryInfo(IntPtr process, out MemoryCounters counters, uint size);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();

        static double WorkingSetBytes()
        {
            var counters = new MemoryCounters { cb = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MemoryCounters)) };
            return GetProcessMemoryInfo(GetCurrentProcess(), out counters, counters.cb) ? counters.WorkingSetSize.ToUInt64() : 0;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        void LateUpdate()
        {
            bool down(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
            if (down(0x11) && down(0x10) && down(0x51)) Application.Quit();   // Ctrl + Shift + Q
        }
#else
        double WorkingSetBytes() => process.WorkingSet64;
#endif
    }
}
