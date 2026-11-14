using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DesktopRider.Core.Desktop;
using DesktopRider.Core.Placement;
using UnityEngine;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Turns the Unity player window into a small, see-through, always-on-top window that is moved about the desktop to
    /// wherever the rider is. Only that small area is drawn, which keeps the cost of drawing low. Clicks pass through to
    /// whatever is behind, except while the mouse is over something that asks for it (<see cref="WantsMouse"/>).
    ///
    /// Positions are "desktop pixels": measured from the top-left corner of the area that holds every screen, so a screen
    /// left of or above the main one does not give negative numbers. <see cref="Area"/> lists the screens.
    /// Inside the Unity editor the Game view stands in for one screen and nothing here touches Windows.
    /// </summary>
    public sealed class OverlayWindow : MonoBehaviour
    {
        [Tooltip("Width and height of the see-through window at 100% screen scaling and normal rider size, in pixels.")]
        public int windowSize = 360;

        /// <summary>Set each frame by whoever knows what is under the mouse. True keeps clicks for the rider.</summary>
        public static bool WantsMouse { get; set; }

        /// <summary>True once the window has been made see-through (always false in the editor).</summary>
        public static bool IsActive { get; private set; }

        /// <summary>The screens the rider can ride on.</summary>
        public static RidingArea Area { get; private set; } = RidingArea.Single(1920, 1080);

        /// <summary>
        /// Windows' screen scaling for the main screen, as a multiple of 100% (1.5 at 150%). The rider and its window are
        /// drawn this much bigger so it looks the same size as other things on screen.
        /// </summary>
        public static float ScaleFactor { get; private set; } = 1f;

        /// <summary>Add to a position as Windows gives it to get a desktop pixel (subtract to go back).</summary>
        public static PixelPoint WindowsOffset { get; private set; }

        /// <summary>Where the window is on the desktop, in desktop pixels.</summary>
        public static RectInt WindowRect { get; private set; } = new RectInt(0, 0, 1920, 1080);

        /// <summary>Mouse position in desktop pixels, read straight from Windows.</summary>
        public static Vector2 MouseDesktop { get; private set; }

        /// <summary>True while the left mouse button is held, wherever the mouse is.</summary>
        public static bool LeftButtonDown { get; private set; }

        /// <summary>True while the right mouse button is held, wherever the mouse is.</summary>
        public static bool RightButtonDown { get; private set; }

        /// <summary>Raised on the main thread after the screens change (one added, removed, moved, or its taskbar moved).</summary>
        public static event Action ScreensChanged;

        /// <summary>Asks for the window's top-left corner to be at this desktop pixel. Does nothing in the editor.</summary>
        public static void PlaceWindow(Vector2Int corner)
        {
            if (instance != null) instance.MoveTo(corner);
        }

        /// <summary>Makes the window a square of this many pixels, keeping its centre where it is.</summary>
        public static void Resize(int size) => Resize(size, size);

        /// <summary>
        /// Changes the window's width and height, in pixels, keeping its centre where it is. A trick uses a window the
        /// shape of the trick, so a long jump does not need a square taller than the screen.
        /// </summary>
        public static void Resize(int width, int height)
        {
            BaseSize = new Vector2Int(Mathf.Max(64, width), Mathf.Max(64, height));
            if (instance != null) instance.SetSize(BaseSize);
        }

        /// <summary>
        /// Makes the window at least this big (for a trick), never smaller. Once grown it stays that size while the rider
        /// rides about in it: shrinking a window shows the last big picture squeezed into it for a moment, a flicker Boss
        /// saw at the end of tricks (14 November); growing can be done cleanly (see <see cref="WindowStep"/>).
        /// </summary>
        public static void Grow(int width, int height)
        {
            if (instance == null) return;
            var now = WantedSize;
            if (width <= now.x && height <= now.y) return;
            instance.SetSize(new Vector2Int(Mathf.Max(now.x, width), Mathf.Max(now.y, height)));
        }

        /// <summary>The size the rider's own window was last given (for his size setting), without any growth for tricks.</summary>
        public static Vector2Int BaseSize { get; private set; }

        /// <summary>The size the window is going to (it can take a frame or two to get there).</summary>
        public static Vector2Int WantedSize => instance != null ? instance.TargetSize : new Vector2Int(WindowRect.width, WindowRect.height);

        /// <summary>Hides or shows the window. While hidden nothing is drawn and no clicks are taken.</summary>
        public static void SetVisible(bool visible)
        {
            if (instance != null) instance.ApplyVisible(visible);
        }

        /// <summary>Keeps the window above other windows, or lets other windows cover it.</summary>
        public static void SetAlwaysOnTop(bool onTop)
        {
            if (instance != null) instance.ApplyTopmost(onTop);
        }

        /// <summary>Reads the screens again. Called when Windows says they changed.</summary>
        public static void RefreshScreens()
        {
            if (instance == null) return;
            instance.ReadScreens();
            ScreensChanged?.Invoke();
        }

        /// <summary>The window's normal size times the given rider size, for the current screen scaling.</summary>
        public int SizeFor(float riderScale) => Mathf.RoundToInt(windowSize * ScaleFactor * Mathf.Max(0.1f, riderScale));

        static OverlayWindow instance;

        void Awake()
        {
            instance = this;
            ReadScreens();
        }
        void OnDestroy() { if (instance == this) instance = null; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        const uint WS_POPUP = 0x80000000, WS_VISIBLE = 0x10000000;
        const uint WS_EX_TOPMOST = 0x00000008, WS_EX_LAYERED = 0x00080000, WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020, SWP_SHOWWINDOW = 0x0040;
        const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4, VK_LBUTTON = 0x01, VK_RBUTTON = 0x02;
        const uint MONITORINFOF_PRIMARY = 1;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1), HWND_NOTOPMOST = new IntPtr(-2);

        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int left, right, top, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MONITORINFOEXW
        {
            public uint cbSize;
            public RECT rcMonitor, rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
        }
        delegate bool MonitorEnumProc(IntPtr monitor, IntPtr hdc, ref RECT rect, IntPtr data);

        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hWnd, uint command);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW")] static extern IntPtr SetClassLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("gdi32.dll")] static extern IntPtr GetStockObject(int index);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int command);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFOEXW info);
        [DllImport("user32.dll")] static extern uint GetDpiForSystem();
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        IntPtr window;
        bool clickThrough, visible = true, topmost = true;
        Vector2Int size;
        Vector2Int placed = new Vector2Int(int.MinValue, int.MinValue);
        PixelPoint offset;   // add to a Windows position to get a desktop pixel

        IEnumerator Start()
        {
            Application.runInBackground = true;
            window = GetActiveWindow();
            if (size.x == 0) size = new Vector2Int(SizeFor(1f), SizeFor(1f));
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            yield return null;
            yield return null;

            ApplyWindowStyle(true);
            // No background brush: when the window grows, Windows would otherwise fill the new part white for a moment
            // before Unity draws into it.
            const int GCLP_HBRBACKGROUND = -10, NULL_BRUSH = 5;
            SetClassLongPtr(window, GCLP_HBRBACKGROUND, GetStockObject(NULL_BRUSH));
            clickThrough = true;
            ApplyExtendedStyle();
            var centre = Area.MainCentre;
            var corner = new Vector2Int(Mathf.RoundToInt(centre.X) - size.x / 2, Mathf.RoundToInt(centre.Y) - size.y / 2);
            SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, corner.x - offset.X, corner.y - offset.Y, size.x, size.y,
                SWP_FRAMECHANGED | (visible ? SWP_SHOWWINDOW : 0) | SWP_NOACTIVATE);
            placed = corner;
            wantedCorner = corner;
            wantedSize = size;
            WindowRect = new RectInt(corner.x, corner.y, size.x, size.y);
            IsActive = true;
            if (!visible) ShowWindow(window, SW_HIDE);
            // Unity (and Windows, while the app is starting) can move the window back into the ordinary window order, which
            // left "Always on top" ticked but not working until it was switched off and on (Boss, 14 November, twice).
            // Putting it back once or twice at start was not enough, so keep checking now and then, and put it back the
            // same way the switch does (off, then on) whenever an ordinary window has got above it.
            var wait = new WaitForSecondsRealtime(TopmostCheckSeconds);
            while (true)
            {
                yield return wait;
                if (IsActive && visible && topmost && DroppedFromTop(out string why))
                {
                    if (topmostFixes++ < 20) Debug.Log("Desktop Rider was no longer on top (" + why + "); putting it back.");
                    SetWindowPos(window, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                    SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                }
            }
        }

        const float TopmostCheckSeconds = 1.5f;
        const uint GW_HWNDPREV = 3;
        int topmostFixes;

        /// <summary>
        /// True if the window has lost its always-on-top place: Windows no longer marks it so, or an ordinary (not
        /// always-on-top) window that can be seen sits above it. Always-on-top windows all come before ordinary ones, so
        /// only the few above it are looked at. Other always-on-top windows above it are left alone: no fighting them.
        /// </summary>
        bool DroppedFromTop(out string why)
        {
            why = null;
            if (((long)GetWindowLongPtr(window, GWL_EXSTYLE) & WS_EX_TOPMOST) == 0) { why = "not marked always on top"; return true; }
            var above = GetWindow(window, GW_HWNDPREV);
            for (int i = 0; i < 500 && above != IntPtr.Zero; i++, above = GetWindow(above, GW_HWNDPREV))
            {
                if (!IsWindowVisible(above)) continue;
                if (((long)GetWindowLongPtr(above, GWL_EXSTYLE) & WS_EX_TOPMOST) == 0) { why = "an ordinary window is above it"; return true; }
            }
            return false;
        }

        /// <summary>No border or title bar, and a see-through background.</summary>
        void ApplyWindowStyle(bool shown)
        {
            SetWindowLongPtr(window, GWL_STYLE, new IntPtr(unchecked((int)(WS_POPUP | (shown ? WS_VISIBLE : 0)))));
            var margins = new MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
            DwmExtendFrameIntoClientArea(window, ref margins);
        }

        /// <summary>
        /// Unity resizes the window at the end of the frame and, while doing so, gives it back a normal title bar and
        /// border, an opaque background and an ordinary place in the window order. Once it has finished, put the
        /// see-through, borderless, always-on-top window back, at the size and place it should have.
        /// </summary>
        IEnumerator RestyleAfterResize()
        {
            yield return null;
            yield return null;
            ApplyWindowStyle(visible);
            ApplyExtendedStyle();
            SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, placed.x - offset.X, placed.y - offset.Y, size.x, size.y,
                SWP_FRAMECHANGED | SWP_NOACTIVATE);
            if (visible) ApplyTopmost(topmost);
            restyle = null;
        }

        Coroutine restyle;

        void ReadScreens()
        {
            try { ScaleFactor = Mathf.Max(1f, GetDpiForSystem() / 96f); }
            catch (EntryPointNotFoundException) { ScaleFactor = 1f; }     // Windows older than 10 (1607)

            var screens = new List<ScreenArea>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref RECT r, IntPtr data) =>
            {
                var info = new MONITORINFOEXW { cbSize = (uint)Marshal.SizeOf<MONITORINFOEXW>() };
                if (GetMonitorInfoW(monitor, ref info))
                {
                    var w = info.rcWork;
                    screens.Add(new ScreenArea(info.szDevice, new PixelRect(w.left, w.top, w.right - w.left, w.bottom - w.top),
                        (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
                }
                return true;
            }, IntPtr.Zero);
            if (screens.Count == 0) return;     // keep what we had rather than ride on nothing
            Area = RidingArea.FromWindows(screens, out offset);
            WindowsOffset = offset;
            if (IsActive) MoveTo(placed, force: true);
        }

        void MoveTo(Vector2Int corner) => MoveTo(corner, force: false);

        void MoveTo(Vector2Int corner, bool force)
        {
            wantedCorner = corner;
            if (!IsActive) return;
            Apply(force);
        }

        void SetSize(Vector2Int newSize)
        {
            if (newSize == wantedSize) return;
            var centre = new Vector2(wantedCorner.x + wantedSize.x * 0.5f, wantedCorner.y + wantedSize.y * 0.5f);
            wantedSize = newSize;
            wantedCorner = new Vector2Int(Mathf.RoundToInt(centre.x) - newSize.x / 2, Mathf.RoundToInt(centre.y) - newSize.y / 2);
            // Before the window is set up, only remember the size: Start sizes the window with it. Asking Unity to resize
            // here (as before) could land after Start had made the window always-on-top, and Unity's resize puts the
            // window back into the ordinary window order.
            if (!IsActive) { size = newSize; return; }
            Apply(false);
        }

        Vector2Int wantedCorner, wantedSize;
        Vector2Int TargetSize => IsActive ? wantedSize : size;
        int behindSince = -1;

        /// <summary>
        /// Moves and sizes the window towards where it is wanted, in the steps <see cref="WindowStep"/> picks so the
        /// picture never jumps or flashes while Unity catches up with a new size (Boss saw a flicker at the end of tricks).
        /// Resizes go straight to Windows; Unity follows (the player is built with a resizable window) and keeps the
        /// see-through, borderless, always-on-top style. Screen.SetResolution would instead turn the window back into an
        /// ordinary opaque one for a few frames, which flashes up as a white box.
        /// </summary>
        void Apply(bool force)
        {
            var now = new WindowBox(placed.x, placed.y, size.x, size.y);
            var wanted = new WindowBox(wantedCorner.x, wantedCorner.y, wantedSize.x, wantedSize.y);
            // If Unity has not followed after a few frames, stop waiting for it rather than leave the window behind.
            bool behind = Screen.width < wanted.Width || Screen.height < wanted.Height;
            if (!behind) behindSince = -1;
            else if (behindSince < 0) behindSince = Time.frameCount;
            var next = behind && Time.frameCount - behindSince > 10 ? wanted : WindowStep.Next(now, wanted, Screen.width, Screen.height);
            if (!force && next.Equals(now)) return;
            bool resized = !next.SameSize(now);
            SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, next.X - offset.X, next.Y - offset.Y, next.Width, next.Height,
                SWP_NOACTIVATE | (resized ? 0 : SWP_NOSIZE));
            placed = new Vector2Int(next.X, next.Y);
            size = new Vector2Int(next.Width, next.Height);
            WindowRect = new RectInt(next.X, next.Y, next.Width, next.Height);
            if (!resized) return;
            if (restyle != null) StopCoroutine(restyle);
            restyle = StartCoroutine(CheckResize());
        }

        /// <summary>
        /// If Unity did not follow the new window size (an older build made without a resizable window), fall back to
        /// asking Unity for it, then put the see-through style back.
        /// </summary>
        IEnumerator CheckResize()
        {
            for (int i = 0; i < 10 && (Screen.width != size.x || Screen.height != size.y); i++) yield return null;
            if (Screen.width == size.x && Screen.height == size.y) { restyle = null; yield break; }
            Debug.LogWarning("Desktop Rider's window did not resize directly; asking Unity to resize it instead.");
            Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            restyle = StartCoroutine(RestyleAfterResize());
        }

        void ApplyVisible(bool show)
        {
            visible = show;
            if (!IsActive) return;
            ShowWindow(window, show ? SW_SHOWNOACTIVATE : SW_HIDE);
            // ShowWindow can drop the always-on-top place; put it back.
            if (show) ApplyTopmost(topmost);
        }

        void ApplyTopmost(bool onTop)
        {
            topmost = onTop;
            if (!IsActive) return;
            SetWindowPos(window, onTop ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        void ApplyExtendedStyle()
        {
            uint style = WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            if (clickThrough) style |= WS_EX_TRANSPARENT;
            if (topmost) style |= WS_EX_TOPMOST;      // keep the always-on-top mark this replaces
            SetWindowLongPtr(window, GWL_EXSTYLE, new IntPtr(unchecked((int)style)));
        }

        void Update()
        {
            if (!IsActive) return;
            if (placed != wantedCorner || size != wantedSize) Apply(false);    // the next step of a change of size
            if (GetCursorPos(out var p)) MouseDesktop = new Vector2(p.x + offset.X, p.y + offset.Y);
            LeftButtonDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;
            RightButtonDown = (GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0;

            bool shouldPassThrough = !WantsMouse || !visible;
            if (shouldPassThrough != clickThrough)
            {
                clickThrough = shouldPassThrough;
                ApplyExtendedStyle();
            }
        }
#else
        Vector2Int TargetSize => new Vector2Int(WindowRect.width, WindowRect.height);
        void MoveTo(Vector2Int corner) { }
        void SetSize(Vector2Int newSize) { }
        void ApplyVisible(bool show) { }
        void ApplyTopmost(bool onTop) { }
        void ReadScreens() { }

        void Update()
        {
            // The Game view is the whole desktop and the window fills it.
            if (Area.Size.Width != Screen.width || Area.Size.Height != Screen.height) Area = RidingArea.Single(Screen.width, Screen.height);
            WindowRect = new RectInt(0, 0, Screen.width, Screen.height);
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            var p = mouse.position.ReadValue();
            MouseDesktop = new Vector2(p.x, Screen.height - p.y);
            LeftButtonDown = mouse.leftButton.isPressed;
            RightButtonDown = mouse.rightButton.isPressed;
        }
#endif
    }
}
