using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Phase 1 measurement: every few seconds writes frame rate, memory and processor use to proof-stats.txt
    /// beside the program. Also closes the program when a file called quit.txt appears there, or on Ctrl+Shift+Q.
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

        void Start()
        {
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
