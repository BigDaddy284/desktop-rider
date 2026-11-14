#nullable enable

using System;
using System.Threading;
using UnityEngine;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Runs work on Unity's main thread. Settings saves and other background work finish on other threads, but Unity and
    /// the Windows window calls must only be used from the main thread, so their follow-up is posted here.
    /// </summary>
    public static class MainThread
    {
        static SynchronizationContext? context;
        static int mainThreadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Capture()
        {
            context = SynchronizationContext.Current;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsCurrent => Thread.CurrentThread.ManagedThreadId == mainThreadId;

        /// <summary>Runs the action now if already on the main thread, otherwise at the start of the next frame.</summary>
        public static void Post(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (IsCurrent || context == null) { action(); return; }
            context.Post(_ => action(), null);
        }
    }
}
