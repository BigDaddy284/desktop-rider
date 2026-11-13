using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Turns the Unity player window into a see-through, always-on-top layer over the main screen.
    /// Clicks pass through to whatever is behind, except while the mouse is over something that asks for it
    /// (see <see cref="WantsMouse"/>). Does nothing inside the Unity editor.
    /// </summary>
    public sealed class OverlayWindow : MonoBehaviour
    {
        /// <summary>Set each frame by whoever knows what is under the mouse. True keeps clicks for the rider.</summary>
        public static bool WantsMouse { get; set; }

        /// <summary>True once the window has been made see-through (always false in the editor).</summary>
        public static bool IsActive { get; private set; }

        /// <summary>Mouse position in Unity screen pixels (origin bottom left), read straight from Windows.</summary>
        public static Vector2 MousePosition { get; private set; }

        /// <summary>True while the left mouse button is held, wherever the mouse is.</summary>
        public static bool LeftButtonDown { get; private set; }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
        const uint WS_POPUP = 0x80000000, WS_VISIBLE = 0x10000000;
        const uint WS_EX_LAYERED = 0x00080000, WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        const uint SWP_FRAMECHANGED = 0x0020, SWP_SHOWWINDOW = 0x0040, SWP_NOACTIVATE = 0x0010;
        const int SM_CXSCREEN = 0, SM_CYSCREEN = 1, VK_LBUTTON = 0x01;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int left, right, top, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }

        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hWnd, ref POINT p);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS margins);

        IntPtr window;
        bool clickThrough;
        int width, height;

        IEnumerator Start()
        {
            Application.runInBackground = true;
            window = GetActiveWindow();
            width = GetSystemMetrics(SM_CXSCREEN);
            height = GetSystemMetrics(SM_CYSCREEN) - 1;   // one pixel short, so Windows does not treat it as a full-screen game
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            yield return null;
            yield return null;

            SetWindowLongPtr(window, GWL_STYLE, new IntPtr(unchecked((int)(WS_POPUP | WS_VISIBLE))));
            var margins = new MARGINS { left = -1, right = -1, top = -1, bottom = -1 };
            DwmExtendFrameIntoClientArea(window, ref margins);
            clickThrough = true;
            ApplyExtendedStyle();
            SetWindowPos(window, HWND_TOPMOST, 0, 0, width, height, SWP_FRAMECHANGED | SWP_SHOWWINDOW | SWP_NOACTIVATE);
            IsActive = true;
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
            if (GetCursorPos(out var p) && ScreenToClient(window, ref p))
                MousePosition = new Vector2(p.x, Screen.height - 1 - p.y);
            LeftButtonDown = (GetAsyncKeyState(VK_LBUTTON) & 0x8000) != 0;

            bool shouldPassThrough = !WantsMouse;
            if (shouldPassThrough != clickThrough)
            {
                clickThrough = shouldPassThrough;
                ApplyExtendedStyle();
            }
        }
#else
        void Update()
        {
            // In the editor the Game view stands in for the desktop, using the ordinary mouse.
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            MousePosition = mouse.position.ReadValue();
            LeftButtonDown = mouse.leftButton.isPressed;
        }
#endif
    }
}
