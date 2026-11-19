#nullable enable

using System;
using System.Runtime.InteropServices;
using DesktopRider.Core.Desktop;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// The settings window, made with plain Windows calls because Unity can only draw into its own (see-through) window.
    /// What it shows and where everything goes come from <see cref="SettingsPanel"/> and <see cref="SettingsLayout"/>; this
    /// class only draws them, in the dark Windows 11 style, and turns clicks and drags into changes. Every change applies
    /// at once, so there is no Save button. Smooth shapes are drawn with GDI+, words with ClearType. Unity's own message
    /// loop delivers the window's messages on the main thread, so nothing here runs in the background. Must be created and
    /// used on Unity's main thread.
    /// </summary>
    public sealed class SettingsWindow : IDisposable
    {
        const string ClassName = "DesktopRider3D.Settings";

        // Windows 11 dark colours (0xRRGGBB); the accent is the rider's own blue.
        const uint Background = 0x202020, Text = 0xFFFFFF, Muted = 0xC8C8C8, Heading = 0x9ED8F5, Line = 0x3A3A3A;
        const uint Accent = 0x4CC2FF, OnAccent = 0x000000, Control = 0x2D2D2D, ControlHover = 0x383838, Border = 0x4A4A4A;
        const uint Off = 0xA0A0A0, Track = 0x8A8A8A;

        readonly SettingsPanel panel;
        SettingsLayout layout;              // worked out again whenever the window changes size
        readonly IntPtr icon;
        IntPtr window, gdiplusToken;
        double scale = 1;
        SettingRow? dragging;
        PanelHit hover;
        DateTime lastDragSave;
        bool disposed;

        static SettingsWindow? current;
        static readonly WndProc Procedure = WindowProcedure;      // kept in a static so it is never collected

        /// <param name="icon">The window's icon (not owned: the caller frees it), or zero for none.</param>
        public SettingsWindow(SettingsPanel panel, IntPtr icon)
        {
            this.panel = panel ?? throw new ArgumentNullException(nameof(panel));
            layout = new SettingsLayout(panel);
            this.icon = icon;
        }

        /// <summary>Raised when "Move to corner" is clicked.</summary>
        public event Action? ResetRequested;

        /// <summary>Raised when "Do a trick" is clicked.</summary>
        public event Action? TrickRequested;

        /// <summary>Raised when a change could not be made; the window keeps working.</summary>
        public event Action<Exception>? CommandFailed;

        public bool IsOpen => window != IntPtr.Zero;

        /// <summary>Opens the window in the middle of the screen the mouse is on, or brings it to the front if it is open.</summary>
        public void Open()
        {
            if (disposed) return;
            if (window != IntPtr.Zero)
            {
                ShowWindow(window, SW_RESTORE);
                SetForegroundWindow(window);
                return;
            }
            if (gdiplusToken == IntPtr.Zero)
            {
                var input = new GdiplusStartupInput { GdiplusVersion = 1 };
                if (GdiplusStartup(out gdiplusToken, ref input, IntPtr.Zero) != 0) gdiplusToken = IntPtr.Zero;
            }

            IntPtr module = GetModuleHandleW(null);
            var cls = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                style = CS_HREDRAW | CS_VREDRAW,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Procedure),
                hInstance = module,
                hCursor = LoadCursorW(IntPtr.Zero, new IntPtr(IDC_ARROW)),
                hIcon = icon,
                hIconSm = icon,
                lpszClassName = ClassName,
            };
            RegisterClassExW(ref cls);       // fails harmlessly if the class is already registered

            // Size it for the screen the mouse is on, so it is sharp at any Windows scaling.
            GetCursorPos(out var mouse);
            IntPtr monitor = MonitorFromPoint(mouse, MONITOR_DEFAULTTONEAREST);
            uint dpi = 96;
            try { if (GetDpiForMonitor(monitor, 0, out uint dx, out _) == 0) dpi = dx; } catch (EntryPointNotFoundException) { } catch (DllNotFoundException) { }
            scale = dpi / 96.0;
            var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfoW(monitor, ref info);

            var frame = new RECT { right = Px(SettingsLayout.Width), bottom = Px(layout.Height) };
            try { AdjustWindowRectExForDpi(ref frame, Style, false, ExStyle, dpi); } catch (EntryPointNotFoundException) { AdjustWindowRectEx(ref frame, Style, false, ExStyle); }
            int w = frame.right - frame.left, h = frame.bottom - frame.top;
            var work = info.rcWork;
            h = Math.Min(h, work.bottom - work.top);       // a short screen opens it shorter, without the descriptions
            int x = work.left + Math.Max(0, ((work.right - work.left) - w) / 2);
            int y = work.top + Math.Max(0, ((work.bottom - work.top) - h) / 2);

            current = this;
            window = CreateWindowExW(ExStyle, ClassName, SettingsPanel.Title, Style, x, y, w, h, IntPtr.Zero, IntPtr.Zero, module, IntPtr.Zero);
            if (window == IntPtr.Zero)
            {
                CommandFailed?.Invoke(new InvalidOperationException("Windows would not make the settings window (error " + Marshal.GetLastWin32Error() + ")."));
                return;
            }

            // Dark title bar and round corners on Windows 11 (older Windows ignores these).
            int on = 1, round = DWMWCP_ROUND;
            DwmSetWindowAttribute(window, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            DwmSetWindowAttribute(window, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            if (icon != IntPtr.Zero)
            {
                SendMessageW(window, WM_SETICON, new IntPtr(ICON_BIG), icon);
                SendMessageW(window, WM_SETICON, new IntPtr(ICON_SMALL), icon);
            }
            ShowWindow(window, SW_SHOWNORMAL);
            SetForegroundWindow(window);
        }

        /// <summary>Draws the window again, for example after a setting changed somewhere else (the tray menu).</summary>
        public void Refresh()
        {
            if (window != IntPtr.Zero) InvalidateRect(window, IntPtr.Zero, false);
        }

        public void Close()
        {
            if (window != IntPtr.Zero) DestroyWindow(window);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Close();
            if (gdiplusToken != IntPtr.Zero) { GdiplusShutdown(gdiplusToken); gdiplusToken = IntPtr.Zero; }
            if (current == this) current = null;
        }

        int Px(double dip) => (int)Math.Round(dip * scale);

        /// <summary>The window changed size: lay everything out again for the new inside size.</summary>
        void Resized()
        {
            if (window == IntPtr.Zero || !GetClientRect(window, out var client) || client.right <= 0 || client.bottom <= 0) return;
            layout = SettingsLayout.Fit(panel, client.right / scale, client.bottom / scale);
            hover = default;
            Refresh();
        }

        /// <summary>How small and how big the window may be dragged: never smaller than the short form, never taller than everything shown.</summary>
        void SizeLimits(IntPtr lParam)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var (minW, minH) = SettingsLayout.SmallestSize(panel);
            var small = Outer(minW, minH);
            var big = Outer(SettingsLayout.MaxWidth, SettingsLayout.FullHeight(panel));
            mmi.ptMinTrackSize = new POINT { x = small.x, y = small.y };
            mmi.ptMaxTrackSize = new POINT { x = big.x, y = Math.Max(small.y, big.y) };
            Marshal.StructureToPtr(mmi, lParam, false);
        }

        /// <summary>The whole window's size, frame included, for an inside of the given size.</summary>
        POINT Outer(double width, double height)
        {
            var r = new RECT { right = Px(width), bottom = Px(height) };
            try { AdjustWindowRectExForDpi(ref r, Style, false, ExStyle, (uint)Math.Round(scale * 96)); } catch (EntryPointNotFoundException) { AdjustWindowRectEx(ref r, Style, false, ExStyle); }
            return new POINT { x = r.right - r.left, y = r.bottom - r.top };
        }

        void Run(Func<System.Threading.Tasks.Task> change)
        {
            try
            {
                var task = change();
                task.ContinueWith(t => { if (t.Exception != null) MainThread.Post(() => CommandFailed?.Invoke(t.Exception.GetBaseException())); },
                                  System.Threading.Tasks.TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex) { CommandFailed?.Invoke(ex); }
            Refresh();
        }

        // ---- mouse ----

        void OnMouseDown(double x, double y)
        {
            var hit = layout.HitTest(x, y);
            if (hit.Button == SettingsLayout.CloseButton) { Close(); return; }
            if (hit.Button == SettingsLayout.ResetButton) { ResetRequested?.Invoke(); return; }
            if (hit.Button == SettingsLayout.TrickButton) { TrickRequested?.Invoke(); return; }
            var row = hit.Row;
            if (row == null) return;
            switch (row.Kind)
            {
                case SettingKind.Toggle:
                    Run(() => row.SetAsync(row.IsOn ? 0 : 1));
                    break;
                case SettingKind.Choice:
                    if (hit.Choice >= 0) Run(() => row.SetAsync(hit.Choice));
                    break;
                case SettingKind.Slider:
                    dragging = row;
                    SetCapture(window);
                    lastDragSave = DateTime.UtcNow;
                    Run(() => row.SetAsync(layout.SliderValueAt(row, x)));
                    break;
            }
        }

        void OnMouseMove(double x, double y)
        {
            if (dragging != null)
            {
                // The rider follows the slider as it moves; saved a few times a second rather than on every pixel.
                if ((DateTime.UtcNow - lastDragSave).TotalMilliseconds < 80) return;
                lastDragSave = DateTime.UtcNow;
                var row = dragging;
                Run(() => row.SetAsync(layout.SliderValueAt(row, x)));
                return;
            }
            var hit = layout.HitTest(x, y);
            if (hit.Row != hover.Row || hit.Choice != hover.Choice || hit.Button != hover.Button)
            {
                hover = hit;
                var track = new TRACKMOUSEEVENT { cbSize = (uint)Marshal.SizeOf<TRACKMOUSEEVENT>(), dwFlags = TME_LEAVE, hwndTrack = window };
                TrackMouseEvent(ref track);
                Refresh();
            }
        }

        void OnMouseUp(double x)
        {
            if (dragging == null) return;
            var row = dragging;
            dragging = null;
            ReleaseCapture();
            Run(() => row.SetAsync(layout.SliderValueAt(row, x)));
        }

        // ---- drawing ----

        void Paint()
        {
            var ps = new PAINTSTRUCT();
            IntPtr dc = BeginPaint(window, out ps);
            try
            {
                GetClientRect(window, out var client);
                IntPtr mem = CreateCompatibleDC(dc);
                IntPtr bmp = CreateCompatibleBitmap(dc, client.right, client.bottom);
                IntPtr oldBmp = SelectObject(mem, bmp);
                try
                {
                    Fill(mem, client, Background);
                    DrawShapes(mem);
                    DrawWords(mem);
                    BitBlt(dc, 0, 0, client.right, client.bottom, mem, 0, 0, SRCCOPY);
                }
                finally
                {
                    SelectObject(mem, oldBmp);
                    DeleteObject(bmp);
                    DeleteDC(mem);
                }
            }
            finally { EndPaint(window, ref ps); }
        }

        void DrawShapes(IntPtr dc)
        {
            if (gdiplusToken == IntPtr.Zero || GdipCreateFromHDC(dc, out var g) != 0) return;
            try
            {
                GdipSetSmoothingMode(g, SmoothingModeAntiAlias);
                GdipSetPixelOffsetMode(g, PixelOffsetModeHalf);

                // the thin line above the buttons
                FillRect(g, new PanelRect(0, layout.FooterLine, layout.PanelWidth, 1), Line);

                foreach (var row in panel.Rows)
                {
                    var c = layout.Control(row);
                    switch (row.Kind)
                    {
                        case SettingKind.Toggle:
                        {
                            bool on = row.IsOn, over = hover.Row == row;
                            if (on) FillRound(g, c, c.Height / 2, over ? Lighter(Accent) : Accent);
                            else { FillRound(g, c, c.Height / 2, over ? ControlHover : Background); DrawRound(g, c, c.Height / 2, Off); }
                            double d = c.Height - 10, kx = on ? c.Right - 5 - d : c.X + 5;
                            FillEllipse(g, new PanelRect(kx, c.Y + 5, d, d), on ? OnAccent : Off);
                            break;
                        }
                        case SettingKind.Choice:
                        {
                            FillRound(g, c, 6, Control);
                            DrawRound(g, c, 6, Border);
                            for (int i = 0; i < row.Choices.Count; i++)
                            {
                                var b = layout.ChoiceButton(row, i);
                                var inner = new PanelRect(b.X + 3, b.Y + 3, b.Width - 6, b.Height - 6);
                                if (row.IsPicked(i)) FillRound(g, inner, 4, Accent);
                                else if (hover.Row == row && hover.Choice == i) FillRound(g, inner, 4, ControlHover);
                            }
                            break;
                        }
                        default:
                        {
                            double f = SettingsLayout.SliderFraction(row), knob = 10, cy = c.Y + (c.Height / 2);
                            double x0 = c.X + knob, x1 = c.Right - knob, kx = x0 + (f * (x1 - x0));
                            FillRound(g, new PanelRect(x0, cy - 2, x1 - x0, 4), 2, Track);
                            FillRound(g, new PanelRect(x0, cy - 2, kx - x0, 4), 2, Accent);
                            FillEllipse(g, new PanelRect(kx - knob, cy - knob, 2 * knob, 2 * knob), 0x454545);
                            double inner = (hover.Row == row || dragging == row) ? 7 : 5.5;
                            FillEllipse(g, new PanelRect(kx - inner, cy - inner, 2 * inner, 2 * inner), Accent);
                            break;
                        }
                    }
                }

                FillRound(g, layout.Reset, 6, hover.Button == SettingsLayout.ResetButton ? ControlHover : Control);
                DrawRound(g, layout.Reset, 6, Border);
                FillRound(g, layout.Trick, 6, hover.Button == SettingsLayout.TrickButton ? ControlHover : Control);
                DrawRound(g, layout.Trick, 6, Border);
                FillRound(g, layout.Close, 6, hover.Button == SettingsLayout.CloseButton ? Lighter(Accent) : Accent);
            }
            finally { GdipDeleteGraphics(g); }
        }

        void DrawWords(IntPtr dc)
        {
            SetBkMode(dc, TRANSPARENT);
            IntPtr title = Font(20, 600), heading = Font(13, 600), name = Font(14, 400), small = Font(12, 400), button = Font(13, 400);
            IntPtr old = SelectObject(dc, title);
            try
            {
                Write(dc, title, SettingsPanel.Title, layout.TitleArea, Text, DT_LEFT | DT_VCENTER | DT_SINGLELINE);
                foreach (var (text, area) in layout.Headings)
                    Write(dc, heading, text, new PanelRect(area.X, area.Y + 10, area.Width, area.Height - 10), Heading, DT_LEFT | DT_TOP | DT_SINGLELINE);

                foreach (var row in panel.Rows)
                {
                    var t = layout.TextArea(row);
                    Write(dc, name, row.Title, new PanelRect(t.X, t.Y, t.Width, 20), Text, DT_LEFT | DT_TOP | DT_SINGLELINE | DT_END_ELLIPSIS);
                    if (layout.ShowsDescriptions)
                        Write(dc, small, row.Description, new PanelRect(t.X, t.Y + 21, t.Width, t.Height - 21), Muted, DT_LEFT | DT_TOP | DT_WORDBREAK | DT_END_ELLIPSIS);
                    if (row.Kind == SettingKind.Choice)
                    {
                        for (int i = 0; i < row.Choices.Count; i++)
                            Write(dc, button, row.Choices[i], layout.ChoiceButton(row, i), row.IsPicked(i) ? OnAccent : Text, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
                    }
                    else if (row.Kind == SettingKind.Slider && layout.ShowsDescriptions)
                    {
                        var l = layout.SliderLabels(row);
                        Write(dc, small, row.LowLabel, l, Muted, DT_LEFT | DT_TOP | DT_SINGLELINE);
                        Write(dc, small, row.HighLabel, l, Muted, DT_RIGHT | DT_TOP | DT_SINGLELINE);
                    }
                }
                Write(dc, button, SettingsLayout.ResetText, layout.Reset, Text, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
                Write(dc, button, SettingsLayout.TrickText, layout.Trick, Text, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
                Write(dc, button, SettingsLayout.CloseText, layout.Close, OnAccent, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
            }
            finally
            {
                SelectObject(dc, old);
                DeleteObject(title); DeleteObject(heading); DeleteObject(name); DeleteObject(small); DeleteObject(button);
            }
        }

        void Write(IntPtr dc, IntPtr font, string text, PanelRect area, uint colour, uint flags)
        {
            SelectObject(dc, font);
            SetTextColor(dc, ColorRef(colour));
            var r = new RECT { left = Px(area.X), top = Px(area.Y), right = Px(area.Right), bottom = Px(area.Bottom) };
            DrawTextW(dc, text, -1, ref r, flags | DT_NOPREFIX);
        }

        IntPtr Font(double size, int weight) =>
            CreateFontW(-Px(size), 0, 0, 0, weight, 0, 0, 0, DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");

        void Fill(IntPtr dc, RECT r, uint colour)
        {
            IntPtr brush = CreateSolidBrush(ColorRef(colour));
            FillRectGdi(dc, ref r, brush);
            DeleteObject(brush);
        }

        void FillRect(IntPtr g, PanelRect r, uint colour)
        {
            GdipCreateSolidFill(Argb(colour), out var brush);
            GdipFillRectangle(g, brush, (float)(r.X * scale), (float)(r.Y * scale), (float)(r.Width * scale), (float)(r.Height * scale));
            GdipDeleteBrush(brush);
        }

        void FillEllipse(IntPtr g, PanelRect r, uint colour)
        {
            GdipCreateSolidFill(Argb(colour), out var brush);
            GdipFillEllipse(g, brush, (float)(r.X * scale), (float)(r.Y * scale), (float)(r.Width * scale), (float)(r.Height * scale));
            GdipDeleteBrush(brush);
        }

        void FillRound(IntPtr g, PanelRect r, double radius, uint colour)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            IntPtr path = RoundPath(r, radius);
            GdipCreateSolidFill(Argb(colour), out var brush);
            GdipFillPath(g, brush, path);
            GdipDeleteBrush(brush);
            GdipDeletePath(path);
        }

        void DrawRound(IntPtr g, PanelRect r, double radius, uint colour)
        {
            IntPtr path = RoundPath(new PanelRect(r.X + 0.5, r.Y + 0.5, r.Width - 1, r.Height - 1), radius);
            GdipCreatePen1(Argb(colour), (float)Math.Max(1, scale), UnitPixel, out var pen);
            GdipDrawPath(g, pen, path);
            GdipDeletePen(pen);
            GdipDeletePath(path);
        }

        IntPtr RoundPath(PanelRect r, double radius)
        {
            float x = (float)(r.X * scale), y = (float)(r.Y * scale), w = (float)(r.Width * scale), h = (float)(r.Height * scale);
            float d = (float)Math.Min(2 * radius * scale, Math.Min(w, h));
            GdipCreatePath(0, out var path);
            GdipAddPathArc(path, x, y, d, d, 180, 90);
            GdipAddPathArc(path, x + w - d, y, d, d, 270, 90);
            GdipAddPathArc(path, x + w - d, y + h - d, d, d, 0, 90);
            GdipAddPathArc(path, x, y + h - d, d, d, 90, 90);
            GdipClosePathFigure(path);
            return path;
        }

        static uint Lighter(uint rgb)
        {
            uint r = (rgb >> 16) & 0xFF, g = (rgb >> 8) & 0xFF, b = rgb & 0xFF;
            r += (255 - r) / 5; g += (255 - g) / 5; b += (255 - b) / 5;
            return (r << 16) | (g << 8) | b;
        }

        static uint Argb(uint rgb) => 0xFF000000 | rgb;
        static uint ColorRef(uint rgb) => ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);

        // ---- Windows ----

        [AOT.MonoPInvokeCallback(typeof(WndProc))]
        static IntPtr WindowProcedure(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            var self = current;
            // The size limits are asked for while the window is still being made, before its handle is known.
            if (self != null && message == WM_GETMINMAXINFO && (hWnd == self.window || self.window == IntPtr.Zero))
            {
                try { self.SizeLimits(lParam); } catch (Exception ex) { self.CommandFailed?.Invoke(ex); }
                return IntPtr.Zero;
            }
            if (self != null && hWnd == self.window)
            {
                try
                {
                    double x = (short)(lParam.ToInt64() & 0xFFFF) / self.scale, y = (short)((lParam.ToInt64() >> 16) & 0xFFFF) / self.scale;
                    switch (message)
                    {
                        case WM_PAINT: self.Paint(); return IntPtr.Zero;
                        case WM_SIZE: self.Resized(); return IntPtr.Zero;
                        case WM_ERASEBKGND: return new IntPtr(1);
                        case WM_LBUTTONDOWN: self.OnMouseDown(x, y); return IntPtr.Zero;
                        case WM_MOUSEMOVE: self.OnMouseMove(x, y); return IntPtr.Zero;
                        case WM_LBUTTONUP: self.OnMouseUp(x); return IntPtr.Zero;
                        case WM_MOUSELEAVE: self.hover = default; self.Refresh(); return IntPtr.Zero;
                        case WM_CAPTURECHANGED: self.dragging = null; break;
                        case WM_KEYDOWN when wParam.ToInt64() == VK_ESCAPE: self.Close(); return IntPtr.Zero;
                        case WM_DPICHANGED:
                        {
                            // Moved to a screen with different scaling: take the size Windows suggests and draw again.
                            self.scale = (wParam.ToInt64() & 0xFFFF) / 96.0;
                            var r = Marshal.PtrToStructure<RECT>(lParam);
                            SetWindowPos(hWnd, IntPtr.Zero, r.left, r.top, r.right - r.left, r.bottom - r.top, SWP_NOZORDER | SWP_NOACTIVATE);
                            self.Refresh();
                            return IntPtr.Zero;
                        }
                        case WM_DESTROY:
                            self.window = IntPtr.Zero;
                            self.dragging = null;
                            self.hover = default;
                            break;
                    }
                }
                catch (Exception ex) { self.CommandFailed?.Invoke(ex); }
            }
            return DefWindowProcW(hWnd, message, wParam, lParam);
        }

        const uint WS_OVERLAPPED = 0x00000000, WS_CAPTION = 0x00C00000, WS_SYSMENU = 0x00080000, WS_MINIMIZEBOX = 0x00020000, WS_THICKFRAME = 0x00040000;
        const uint WS_EX_APPWINDOW = 0x00040000;
        const uint Style = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_THICKFRAME, ExStyle = WS_EX_APPWINDOW;
        const uint CS_HREDRAW = 0x0002, CS_VREDRAW = 0x0001;
        const uint WM_DESTROY = 0x0002, WM_SIZE = 0x0005, WM_GETMINMAXINFO = 0x0024, WM_PAINT = 0x000F, WM_ERASEBKGND = 0x0014, WM_SETICON = 0x0080, WM_KEYDOWN = 0x0100;
        const uint WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_CAPTURECHANGED = 0x0215;
        const uint WM_MOUSELEAVE = 0x02A3, WM_DPICHANGED = 0x02E0;
        const long VK_ESCAPE = 0x1B;
        const int ICON_SMALL = 0, ICON_BIG = 1, IDC_ARROW = 32512, SW_SHOWNORMAL = 1, SW_RESTORE = 9;
        const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, TME_LEAVE = 0x00000002, MONITOR_DEFAULTTONEAREST = 2;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20, DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;
        const int TRANSPARENT = 1, DEFAULT_CHARSET = 1, CLEARTYPE_QUALITY = 5;
        const uint SRCCOPY = 0x00CC0020;
        const uint DT_LEFT = 0x0, DT_CENTER = 0x1, DT_RIGHT = 0x2, DT_VCENTER = 0x4, DT_TOP = 0x0, DT_WORDBREAK = 0x10;
        const uint DT_SINGLELINE = 0x20, DT_NOPREFIX = 0x800, DT_END_ELLIPSIS = 0x8000;
        const int SmoothingModeAntiAlias = 4, PixelOffsetModeHalf = 4, UnitPixel = 2;

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

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)] struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }
        [StructLayout(LayoutKind.Sequential)] struct MONITORINFO { public uint cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
        [StructLayout(LayoutKind.Sequential)] struct TRACKMOUSEEVENT { public uint cbSize, dwFlags; public IntPtr hwndTrack; public uint dwHoverTime; }
        [StructLayout(LayoutKind.Sequential)] struct GdiplusStartupInput { public uint GdiplusVersion; public IntPtr DebugEventCallback; public int SuppressBackgroundThread, SuppressExternalCodecs; }

        [StructLayout(LayoutKind.Sequential)]
        struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public int fErase;
            public RECT rcPaint;
            public int fRestore, fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern ushort RegisterClassExW(ref WNDCLASSEXW cls);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, bool erase);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT ps);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll", EntryPoint = "FillRect")] static extern int FillRectGdi(IntPtr dc, ref RECT rect, IntPtr brush);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int DrawTextW(IntPtr dc, string text, int count, ref RECT rect, uint format);
        [DllImport("user32.dll")] static extern IntPtr LoadCursorW(IntPtr instance, IntPtr name);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(POINT p, uint flags);
        [DllImport("user32.dll")] static extern bool GetMonitorInfoW(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] static extern bool AdjustWindowRectEx(ref RECT rect, uint style, bool menu, uint exStyle);
        [DllImport("user32.dll")] static extern bool AdjustWindowRectExForDpi(ref RECT rect, uint style, bool menu, uint exStyle, uint dpi);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] static extern IntPtr SetCapture(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT track);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hWnd, int attribute, ref int value, int size);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string? name);

        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int w, int h);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("gdi32.dll")] static extern IntPtr CreateSolidBrush(uint colour);
        [DllImport("gdi32.dll")] static extern int SetBkMode(IntPtr dc, int mode);
        [DllImport("gdi32.dll")] static extern uint SetTextColor(IntPtr dc, uint colour);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline, uint strikeOut,
                                         uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string face);

        [DllImport("gdiplus.dll")] static extern int GdiplusStartup(out IntPtr token, ref GdiplusStartupInput input, IntPtr output);
        [DllImport("gdiplus.dll")] static extern void GdiplusShutdown(IntPtr token);
        [DllImport("gdiplus.dll")] static extern int GdipCreateFromHDC(IntPtr dc, out IntPtr graphics);
        [DllImport("gdiplus.dll")] static extern int GdipDeleteGraphics(IntPtr graphics);
        [DllImport("gdiplus.dll")] static extern int GdipSetSmoothingMode(IntPtr graphics, int mode);
        [DllImport("gdiplus.dll")] static extern int GdipSetPixelOffsetMode(IntPtr graphics, int mode);
        [DllImport("gdiplus.dll")] static extern int GdipCreateSolidFill(uint argb, out IntPtr brush);
        [DllImport("gdiplus.dll")] static extern int GdipDeleteBrush(IntPtr brush);
        [DllImport("gdiplus.dll")] static extern int GdipCreatePen1(uint argb, float width, int unit, out IntPtr pen);
        [DllImport("gdiplus.dll")] static extern int GdipDeletePen(IntPtr pen);
        [DllImport("gdiplus.dll")] static extern int GdipCreatePath(int fillMode, out IntPtr path);
        [DllImport("gdiplus.dll")] static extern int GdipDeletePath(IntPtr path);
        [DllImport("gdiplus.dll")] static extern int GdipAddPathArc(IntPtr path, float x, float y, float w, float h, float start, float sweep);
        [DllImport("gdiplus.dll")] static extern int GdipClosePathFigure(IntPtr path);
        [DllImport("gdiplus.dll")] static extern int GdipFillPath(IntPtr graphics, IntPtr brush, IntPtr path);
        [DllImport("gdiplus.dll")] static extern int GdipDrawPath(IntPtr graphics, IntPtr pen, IntPtr path);
        [DllImport("gdiplus.dll")] static extern int GdipFillEllipse(IntPtr graphics, IntPtr brush, float x, float y, float w, float h);
        [DllImport("gdiplus.dll")] static extern int GdipFillRectangle(IntPtr graphics, IntPtr brush, float x, float y, float w, float h);
    }
}
