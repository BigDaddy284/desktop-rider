using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Turns the Unity player window into a small, see-through, always-on-top window that is moved about the desktop to
    /// wherever the rider is. Only that small area is drawn, which keeps the cost of drawing low. Clicks pass through to
    /// whatever is behind, except while the mouse is over something that asks for it (<see cref="WantsMouse"/>).
    /// Inside the Unity editor the Game view stands in for the desktop and nothing here touches Windows.
    /// </summary>
    public sealed class OverlayWindow : MonoBehaviour
    {
        [Tooltip("Width and height of the see-through window, in screen pixels.")]
        public int windowSize = 360;

        /// <summary>Set each frame by whoever knows what is under the mouse. True keeps clicks for the rider.</summary>
        public static bool WantsMouse { get; set; }

        /// <summary>True once the window has been made see-through (always false in the editor).</summary>
        public static bool IsActive { get; private set; }

        /// <summary>Size of the desktop the rider rides on, in pixels.</summary>
        public static Vector2 DesktopSize { get; private set; } = new Vector2(1920, 1080);

        /// <summary>Where the window is on the desktop, in pixels from the top-left corner.</summary>
        public static RectInt WindowRect { get; private set; } = new RectInt(0, 0, 1920, 1080);

        /// <summary>Mouse position on the desktop, in pixels from the top-left corner, read straight from Windows.</summary>
        public static Vector2 MouseDesktop { get; private set; }

        /// <summary>True while the left mouse button is held, wherever the mouse is.</summary>
        public static bool LeftButtonDown { get; private set; }

        /// <summary>Asks for the window's top-left corner to be at this desktop pixel. Does nothing in the editor.</summary>
        public static void PlaceWindow(Vector2Int corner)
        {
            if (instance != null) instance.MoveTo(corner);
        }

        static OverlayWindow instance;

        void Awake() => instance = this;
        void OnDestroy() { if (instance == this) instance = null; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        const uint WS_POPUP = 0x80000000, WS_VISIBLE = 0x10000000;
        const uint WS_EX_LAYERED = 0x00080000, WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        const uint SWP_FRAMECHANGED = 0x0020, SWP_SHOWWINDOW = 0x0040, SWP_NOACTIVATE = 0x0010, SWP_NOSIZE = 0x0001;
        const int SM_CXSCREEN = 0, SM_CYSCREEN = 1, VK_LBUTTON = 0x01;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int left, right, top, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }

        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        IntPtr window;
        bool clickThrough;
        Vector2Int placed = new Vector2Int(int.MinValue, int.MinValue);

        IEnumerator Start()
        {
            Application.runInBackground = true;
            window = GetActiveWindow();
            DesktopSize = new Vector2(GetSystemMetrics(SM_CXSCREEN), GetSystemMetrics(SM_CYSCREEN));
            Screen.SetResolution(windowSize, windowSize, FullScreenMode.Windowed);
            yield return null;
            yield return null;

            SetWindowLongPtr(window, GWL_STYLE, new IntPtr(unchecked((int)(WS_POPUP | WS_VISIBLE))));
            var margins = new MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
            DwmExtendFrameIntoClientArea(window, ref margins);
            clickThrough = true;
            ApplyExtendedStyle();
            var corner = new Vector2Int((int)(DesktopSize.x - windowSize) / 2, (int)(DesktopSize.y - windowSize) / 2);
            SetWindowPos(window, HWND_TOPMOST, corner.x, corner.y, windowSize, windowSize, SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOACTIVATE);
            placed = corner;
            WindowRect = new RectInt(corner.x, corner.y, windowSize, windowSize);
            IsActive = true;
        }

        void MoveTo(Vector2Int corner)
        {
            if (!IsActive || corner == placed) return;
            SetWindowPos(window, HWND_TOPMOST, corner.x, corner.y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
            placed = corner;
            WindowRect = new RectInt(corner.x, corner.y, windowSize, windowSize);
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
            if (GetCursorPos(out var p)) MousePositionChanged(new Vector2(p.x, p.y));
            LeftButtonDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

            bool shouldPassThrough = !WantsMouse;
            if (shouldPassThrough != clickThrough)
            {
                clickThrough = shouldPassThrough;
                ApplyExtendedStyle();
            }
        }

        static void MousePositionChanged(Vector2 desktop) => MouseDesktop = desktop;
#else
        void MoveTo(Vector2Int corner) { }

        void Update()
        {
            // The Game view is the whole desktop and the window fills it.
            DesktopSize = new Vector2(Screen.width, Screen.height);
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
