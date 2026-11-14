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

        /// <summary>Raised on the main thread after the screens change (one added, removed, moved, or its taskbar moved).</summary>
        public static event Action ScreensChanged;

        /// <summary>Asks for the window's top-left corner to be at this desktop pixel. Does nothing in the editor.</summary>
        public static void PlaceWindow(Vector2Int corner)
        {
            if (instance != null) instance.MoveTo(corner);
        }

        /// <summary>Changes the window's width and height, in pixels, keeping its centre where it is.</summary>
        public static void Resize(int size)
        {
            if (instance != null) instance.SetSize(Mathf.Max(64, size));
        }

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
        const uint WS_EX_LAYERED = 0x00080000, WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020, SWP_SHOWWINDOW = 0x0040;
        const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4, VK_LBUTTON = 0x01;
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
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int command);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFOEXW info);
        [DllImport("user32.dll")] static extern uint GetDpiForSystem();
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        IntPtr window;
        bool clickThrough, visible = true, topmost = true;
        int size;
        Vector2Int placed = new Vector2Int(int.MinValue, int.MinValue);
        PixelPoint offset;   // add to a Windows position to get a desktop pixel

        IEnumerator Start()
        {
            Application.runInBackground = true;
            window = GetActiveWindow();
            if (size == 0) size = SizeFor(1f);
            Screen.SetResolution(size, size, FullScreenMode.Windowed);
            yield return null;
            yield return null;

            ApplyWindowStyle();
            clickThrough = true;
            ApplyExtendedStyle();
            var centre = Area.MainCentre;
            var corner = new Vector2Int(Mathf.RoundToInt(centre.X) - size / 2, Mathf.RoundToInt(centre.Y) - size / 2);
            SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, corner.x - offset.X, corner.y - offset.Y, size, size,
                SWP_FRAMECHANGED | (visible ? SWP_SHOWWINDOW : 0) | SWP_NOACTIVATE);
            placed = corner;
            WindowRect = new RectInt(corner.x, corner.y, size, size);
            IsActive = true;
            if (!visible) ShowWindow(window, SW_HIDE);
        }

        /// <summary>No border or title bar, and a see-through background.</summary>
        void ApplyWindowStyle()
        {
            SetWindowLongPtr(window, GWL_STYLE, new IntPtr(unchecked((int)(WS_POPUP | (visible ? WS_VISIBLE : 0)))));
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
            ApplyWindowStyle();
            ApplyExtendedStyle();
            SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, placed.x - offset.X, placed.y - offset.Y, size, size,
                SWP_FRAMECHANGED | SWP_NOACTIVATE | (visible ? SWP_SHOWWINDOW : 0));
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
            if (!IsActive || (!force && corner == placed)) return;
            SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, corner.x - offset.X, corner.y - offset.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
            placed = corner;
            WindowRect = new RectInt(corner.x, corner.y, size, size);
        }

        void SetSize(int newSize)
        {
            if (newSize == size) return;
            var centre = WindowRect.center;
            size = newSize;
            Screen.SetResolution(size, size, FullScreenMode.Windowed);
            if (!IsActive) return;
            var corner = new Vector2Int(Mathf.RoundToInt(centre.x) - size / 2, Mathf.RoundToInt(centre.y) - size / 2);
            SetWindowPos(window, topmost ? HWND_TOPMOST : HWND_NOTOPMOST, corner.x - offset.X, corner.y - offset.Y, size, size, SWP_NOACTIVATE);
            placed = corner;
            WindowRect = new RectInt(corner.x, corner.y, size, size);
            if (restyle != null) StopCoroutine(restyle);
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
            SetWindowLongPtr(window, GWL_EXSTYLE, new IntPtr(unchecked((int)style)));
        }

        void Update()
        {
            if (!IsActive) return;
            if (GetCursorPos(out var p)) MouseDesktop = new Vector2(p.x + offset.X, p.y + offset.Y);
            LeftButtonDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

            bool shouldPassThrough = !WantsMouse || !visible;
            if (shouldPassThrough != clickThrough)
            {
                clickThrough = shouldPassThrough;
                ApplyExtendedStyle();
            }
        }
#else
        void MoveTo(Vector2Int corner) { }
        void SetSize(int newSize) { }
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
        }
#endif
    }
}
