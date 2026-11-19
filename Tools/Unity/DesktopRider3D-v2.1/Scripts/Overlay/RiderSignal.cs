using System;
using System.IO;
using System.Threading;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// A named Windows event that one Desktop Rider window sets and another waits on, for the few things the riders' separate
    /// windows ask of each other: an extra rider's right-click opens the settings in the main program, and "Do a trick" in
    /// the main program's tray menu can be passed to an extra rider. The waiting costs nothing (no polling).
    /// </summary>
    public sealed class RiderSignal : IDisposable
    {
        public const string OpenSettings = "DesktopRider.OpenSettings";

        /// <summary>The signal that asks the extra rider <paramref name="who"/> to do a trick.</summary>
        public static string DoATrick(DesktopRider.Core.Configuration.RiderCharacter who) => "DesktopRider.DoATrick." + who;

        readonly EventWaitHandle signal;
        readonly RegisteredWaitHandle wait;

        RiderSignal(string name, Action action)
        {
            signal = new EventWaitHandle(false, EventResetMode.AutoReset, name);
            wait = ThreadPool.RegisterWaitForSingleObject(signal, (state, timedOut) => action(), null, Timeout.Infinite, executeOnlyOnce: false);
        }

        /// <summary>Runs <paramref name="action"/> (on a pool thread) each time the signal is set; null if it cannot be made.</summary>
        public static RiderSignal Listen(string name, Action action, Action<Exception> failed)
        {
            try { return new RiderSignal(name, action); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is WaitHandleCannotBeOpenedException)
            {
                failed?.Invoke(ex);
                return null;
            }
        }

        /// <summary>Sets the signal. False when nobody is listening for it.</summary>
        public static bool Send(string name)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(name);
                signal.Set();
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is WaitHandleCannotBeOpenedException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            wait.Unregister(null);
            signal.Dispose();
        }
    }
}
