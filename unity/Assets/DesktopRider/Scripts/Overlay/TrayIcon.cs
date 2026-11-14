#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DesktopRider.Core.Desktop;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Desktop Rider's icon in the notification area beside the clock, made with plain Windows calls because Unity has no
    /// tray support. What the icon shows and does comes from <see cref="TrayViewModel"/>; this class only carries it out.
    /// It owns a hidden window that receives the icon's clicks, and Windows' notices that screens were added, removed or
    /// changed (<see cref="DisplaysChanged"/>). Unity's own message loop delivers those, on the main thread, so nothing here
    /// polls or runs in the background. Must be created and used on Unity's main thread.
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        const string ClassName = "DesktopRider3D.Tray";
        const int MaxTipLength = 127;

        readonly TrayViewModel viewModel;
        readonly IntPtr image;
        readonly bool ownsImage;
        IntPtr window;
        bool shown, disposed;

        static TrayIcon? current;
        static readonly WndProc Procedure = WindowProcedure;      // kept in a static so it is never collected
        static uint taskbarCreated;

        /// <param name="icon">A Windows icon handle that this object then owns and frees, or zero for the standard application icon.</param>
        public TrayIcon(TrayViewModel viewModel, IntPtr icon)
        {
            this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            ownsImage = icon != IntPtr.Zero;
            image = ownsImage ? icon : LoadIcon(IntPtr.Zero, new IntPtr(IDI_APPLICATION));
            viewModel.HintRequested += OnHintRequested;
        }

        /// <summary>Raised when screens are added, removed, moved, resized or their taskbars change.</summary>
        public event Action? DisplaysChanged;

        /// <summary>Raised when an action chosen from the menu throws. The menu itself keeps working.</summary>
        public event Action<Exception>? CommandFailed;

        /// <summary>Shows the icon. Returns false if Windows would not show it; the rider can still be closed with Ctrl+Shift+Q.</summary>
        public bool Show()
        {
            if (disposed) return false;
            if (window == IntPtr.Zero && !CreateWindow()) return false;
            current = this;
            var data = NewData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
            shown = Shell_NotifyIconW(NIM_ADD, ref data);
            return shown;
        }

        /// <summary>Shows a short message beside the icon, as the rider does when it is first hidden.</summary>
        public void ShowHint(string title, string message)
        {
            if (!shown) return;
            var data = NewData(NIF_INFO | NIF_TIP);
            data.szInfoTitle = Cut(title, 63);
            data.szInfo = Cut(message, 255);
            data.dwInfoFlags = NIIF_NONE;
            Shell_NotifyIconW(NIM_MODIFY, ref data);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            viewModel.HintRequested -= OnHintRequested;
            if (shown)
            {
                // Removing it first means no ghost icon is left until the mouse passes over it.
                var data = NewData(0);
                Shell_NotifyIconW(NIM_DELETE, ref data);
                shown = false;
            }
            if (window != IntPtr.Zero) { DestroyWindow(window); window = IntPtr.Zero; }
            if (current == this) current = null;
            if (ownsImage) DestroyIcon(image);
        }

        bool CreateWindow()
        {
            IntPtr module = GetModuleHandleW(null);
            var cls = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Procedure),
                hInstance = module,
                lpszClassName = ClassName,
            };
            RegisterClassExW(ref cls);       // fails harmlessly if the class is already registered
            taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
            // A hidden top-level window rather than a message-only one, so it hears the screen-change broadcasts.
            window = CreateWindowExW(WS_EX_TOOLWINDOW, ClassName, "Desktop Rider", WS_POPUP, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, module, IntPtr.Zero);
            return window != IntPtr.Zero;
        }

        NOTIFYICONDATAW NewData(uint flags) => new NOTIFYICONDATAW
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = window,
            uID = 1,
            uFlags = flags,
            uCallbackMessage = CallbackMessage,
            hIcon = image,
            szTip = Cut(viewModel.ToolTip, MaxTipLength),
            szInfo = string.Empty,
            szInfoTitle = string.Empty,
        };

        static string Cut(string text, int max) => text.Length > max ? text.Substring(0, max) : text;

        void OnHintRequested(object? sender, TrayHintEventArgs e) =>
            // The view model can raise this from a save finishing on another thread; Windows calls belong on the main thread.
            MainThread.Post(() => ShowHint(e.Title, e.Message));

        void OnIconMessage(uint mouseMessage)
        {
            switch (mouseMessage)
            {
                case WM_LBUTTONUP:
                    Run(() => viewModel.DefaultCommand.Execute(null));
                    break;
                case WM_RBUTTONUP:
                    ShowMenu();
                    break;
                case NIN_BALLOONUSERCLICK:
                    Run(() => viewModel.HintClickedCommand.Execute(null));
                    break;
            }
        }

        void ShowMenu()
        {
            var plan = new TrayMenuPlan(viewModel.BuildMenu());
            IntPtr menu = CreatePopupMenu();
            try
            {
                Fill(menu, plan.Lines);
                GetCursorPos(out var at);
                // Without this the menu does not close when the user clicks somewhere else (a long-standing Windows rule).
                SetForegroundWindow(window);
                int chosen = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_NONOTIFY, at.x, at.y, window, IntPtr.Zero);
                PostMessageW(window, WM_NULL, IntPtr.Zero, IntPtr.Zero);
                if (chosen != 0) Run(() => plan.Run(chosen));
            }
            finally
            {
                // Destroying the top menu also destroys the sub-menus inside it.
                DestroyMenu(menu);
            }
        }

        static void Fill(IntPtr menu, IReadOnlyList<TrayMenuPlan.Line> lines)
        {
            foreach (var line in lines)
            {
                if (line.IsSeparator) { AppendMenuW(menu, MF_SEPARATOR, UIntPtr.Zero, null); continue; }
                uint flags = line.IsChecked ? MF_CHECKED : 0u;
                if (line.Children is { } children)
                {
                    IntPtr sub = CreatePopupMenu();
                    Fill(sub, children);
                    AppendMenuW(menu, flags | MF_POPUP, (UIntPtr)(ulong)sub.ToInt64(), line.Text);
                }
                else
                {
                    AppendMenuW(menu, flags | MF_STRING, (UIntPtr)(uint)line.Id, line.Text);
                }
            }
        }

        void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { CommandFailed?.Invoke(ex); }
        }

        [AOT.MonoPInvokeCallback(typeof(WndProc))]
        static IntPtr WindowProcedure(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            var icon = current;
            if (icon != null && hWnd == icon.window)
            {
                if (message == CallbackMessage)
                {
                    icon.OnIconMessage((uint)(lParam.ToInt64() & 0xFFFF));
                    return IntPtr.Zero;
                }
                if (message == WM_DISPLAYCHANGE || (message == WM_SETTINGCHANGE && wParam.ToInt64() == SPI_SETWORKAREA))
                {
                    icon.DisplaysChanged?.Invoke();
                }
                else if (taskbarCreated != 0 && message == taskbarCreated)
                {
                    // Explorer restarted and forgot every icon; put ours back.
                    icon.shown = false;
                    icon.Show();
                }
            }
            return DefWindowProcW(hWnd, message, wParam, lParam);
        }

        // ---- Windows ----

        const uint CallbackMessage = 0x8000 + 0x45;   // WM_APP + a number of our choosing
        const uint WM_NULL = 0x0000, WM_SETTINGCHANGE = 0x001A, WM_DISPLAYCHANGE = 0x007E;
        const uint WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, NIN_BALLOONUSERCLICK = 0x0405;
        const long SPI_SETWORKAREA = 0x002F;
        const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
        const uint NIF_MESSAGE = 0x01, NIF_ICON = 0x02, NIF_TIP = 0x04, NIF_INFO = 0x10;
        const uint NIIF_NONE = 0;
        const uint MF_STRING = 0x0000, MF_CHECKED = 0x0008, MF_POPUP = 0x0010, MF_SEPARATOR = 0x0800;
        const uint TPM_RIGHTBUTTON = 0x0002, TPM_NONOTIFY = 0x0080, TPM_RETURNCMD = 0x0100;
        const uint WS_POPUP = 0x80000000, WS_EX_TOOLWINDOW = 0x00000080;
        const int IDI_APPLICATION = 32512;

        delegate IntPtr WndProc(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WNDCLASSEXW
        {
            public uint cbSize, style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra, cbWndExtra;
            public IntPtr hInstance, hIcon, hCursor, hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
            public IntPtr hIconSm;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct NOTIFYICONDATAW
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID, uFlags, uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public uint dwState, dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATAW data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClassExW(ref WNDCLASSEXW cls);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessageW(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool AppendMenuW(IntPtr menu, uint flags, UIntPtr id, string? text);
        [DllImport("user32.dll")] static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hWnd, IntPtr tpm);
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool PostMessageW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern IntPtr LoadIcon(IntPtr instance, IntPtr name);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string? name);
    }
}
