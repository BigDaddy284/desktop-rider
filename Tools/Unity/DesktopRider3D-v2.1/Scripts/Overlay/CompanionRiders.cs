using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DesktopRider.Core.Configuration;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// The main program's side of "several riders at once": keeps one companion window (a second copy of the program,
    /// started with "--companion Name --host id") running for each extra rider out, and closes the ones sent home. The
    /// companions close themselves when the main program closes, however it closes. A companion that closes by itself is
    /// not started again until the riders out change, so a broken one cannot be restarted over and over.
    /// </summary>
    public sealed class CompanionRiders : IDisposable
    {
        readonly string exe;
        readonly Action<string, Exception> warn;
        readonly Dictionary<RiderCharacter, Process> running = new Dictionary<RiderCharacter, Process>();
        List<RiderCharacter> wanted = new List<RiderCharacter>();

        public CompanionRiders(string exe, Action<string, Exception> warn)
        {
            this.exe = exe;
            this.warn = warn ?? ((m, e) => { });
        }

        /// <summary>The companions running now.</summary>
        public IReadOnlyCollection<RiderCharacter> Running
        {
            get { lock (running) return running.Keys.ToList(); }
        }

        /// <summary>Starts and closes companions so exactly <paramref name="riders"/> have windows of their own.</summary>
        public void Match(IReadOnlyList<RiderCharacter> riders)
        {
            if (riders.SequenceEqual(wanted)) return;
            wanted = riders.ToList();
            List<RiderCharacter> stop;
            lock (running) stop = running.Keys.Where(c => !riders.Contains(c)).ToList();
            foreach (var who in stop) Close(who);
            foreach (var who in riders) Start(who);
        }

        void Start(RiderCharacter who)
        {
            lock (running) if (running.ContainsKey(who)) return;
            try
            {
                int host;
                using (var me = Process.GetCurrentProcess()) host = me.Id;
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo(exe, RiderLineUp.CompanionArguments(who, host) + LogFileFor(who))
                    {
                        UseShellExecute = false,
                        WorkingDirectory = System.IO.Path.GetDirectoryName(exe) ?? string.Empty,
                    },
                    EnableRaisingEvents = true,
                };
                process.Exited += (s, e) => { lock (running) if (running.TryGetValue(who, out var p) && p == process) running.Remove(who); process.Dispose(); };
                process.Start();
                lock (running) running[who] = process;
                UnityEngine.Debug.Log("Companion window started for " + who + " (process " + process.Id + ").");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is System.IO.IOException)
            {
                warn(who + " could not be brought out in a window of their own.", ex);
            }
        }

        /// <summary>Each companion keeps its own Unity log (Player-Louie.log beside Player.log), so they do not write over the main one.</summary>
        static string LogFileFor(RiderCharacter who)
        {
            string main = UnityEngine.Application.consoleLogPath;
            if (string.IsNullOrEmpty(main)) return string.Empty;
            return " -logFile \"" + System.IO.Path.Combine(System.IO.Path.GetDirectoryName(main) ?? string.Empty, "Player-" + who + ".log") + "\"";
        }

        void Close(RiderCharacter who)
        {
            Process process;
            lock (running)
            {
                if (!running.TryGetValue(who, out process)) return;
                running.Remove(who);
            }
            try
            {
                if (!process.HasExited) { process.Kill(); process.WaitForExit(2000); }   // gone before one is started again
                UnityEngine.Debug.Log("Companion window closed for " + who + ".");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                // Already gone.
            }
        }

        /// <summary>Closes and starts again every companion, which puts them back beside the middle of the main screen.</summary>
        public void Restart()
        {
            var again = wanted;
            Dispose();
            Match(again);
        }

        /// <summary>Closes every companion (the main program is closing).</summary>
        public void Dispose()
        {
            List<RiderCharacter> all;
            lock (running) all = running.Keys.ToList();
            foreach (var who in all) Close(who);
            wanted = new List<RiderCharacter>();
        }
    }
}
