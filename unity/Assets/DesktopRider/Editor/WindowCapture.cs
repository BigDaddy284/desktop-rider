using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DesktopRider.EditorTools
{
    /// <summary>
    /// Developer checks of Desktop Rider's own windows from the editor bridge, without touching the screen: saves a picture
    /// of a window found by its title (Windows' PrintWindow, so it works even behind other windows), and sends a mouse click
    /// to a point in it. Used to check the settings window. Editor only; never part of the program.
    /// </summary>
    static class WindowCapture
    {
        /// <summary>"title" saves the window to <paramref name="file"/>.</summary>
        public static string Capture(string title, string file)
        {
            IntPtr hWnd = FindWindowW(null, title);
            if (hWnd == IntPtr.Zero) throw new ArgumentException("No window called " + title);
            GetWindowRect(hWnd, out var r);
            int w = r.right - r.left, h = r.bottom - r.top;
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr dc = CreateCompatibleDC(screen);
            var info = new BITMAPINFO { biSize = 40, biWidth = w, biHeight = h, biPlanes = 1, biBitCount = 32 };
            IntPtr bmp = CreateDIBSection(dc, ref info, 0, out IntPtr bits, IntPtr.Zero, 0);
            IntPtr old = SelectObject(dc, bmp);
            try
            {
                PrintWindow(hWnd, dc, PW_RENDERFULLCONTENT);
                var pixels = new byte[w * h * 4];
                Marshal.Copy(bits, pixels, 0, pixels.Length);
                for (int i = 0; i < pixels.Length; i += 4) { byte b = pixels[i]; pixels[i] = pixels[i + 2]; pixels[i + 2] = b; pixels[i + 3] = 255; }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(pixels);
                tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally
            {
                SelectObject(dc, old);
                DeleteObject(bmp);
                DeleteDC(dc);
                ReleaseDC(IntPtr.Zero, screen);
            }
            return $"saved {w} x {h} to {file}";
        }

        /// <summary>
        /// "title|count|ms": grabs the screen (as composed, see-through windows included) over the window and 150 pixels round it
        /// <paramref name="count"/> times, <paramref name="ms"/> apart, into prefix-N.png. Catches single frames.
        /// </summary>
        public static string ScreenGrab(string argument, string prefix)
        {
            var parts = argument.Split('|');
            IntPtr hWnd = FindWindowW(null, parts[0]);
            if (hWnd == IntPtr.Zero) throw new ArgumentException("No window called " + parts[0]);
            int count = int.Parse(parts[1]), ms = int.Parse(parts[2]);
            GetWindowRect(hWnd, out var r);
            int x0 = r.left - 150, y0 = r.top - 150, w = r.right - r.left + 300, h = r.bottom - r.top + 300;   // the window and a margin
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr dc = CreateCompatibleDC(screen);
            var info = new BITMAPINFO { biSize = 40, biWidth = w, biHeight = h, biPlanes = 1, biBitCount = 32 };
            IntPtr bmp = CreateDIBSection(dc, ref info, 0, out IntPtr bits, IntPtr.Zero, 0);
            IntPtr old = SelectObject(dc, bmp);
            var shots = new System.Collections.Generic.List<byte[]>();
            try
            {
                for (int k = 0; k < count; k++)
                {
                    BitBlt(dc, 0, 0, w, h, screen, x0, y0, 0x00CC0020 | 0x40000000);   // SRCCOPY | CAPTUREBLT
                    var pixels = new byte[w * h * 4];
                    Marshal.Copy(bits, pixels, 0, pixels.Length);
                    shots.Add(pixels);
                    System.Threading.Thread.Sleep(ms);
                }
            }
            finally
            {
                SelectObject(dc, old);
                DeleteObject(bmp);
                DeleteDC(dc);
                ReleaseDC(IntPtr.Zero, screen);
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int k = 0; k < shots.Count; k++)
            {
                var pixels = shots[k];
                for (int i = 0; i < pixels.Length; i += 4) { byte b = pixels[i]; pixels[i] = pixels[i + 2]; pixels[i + 2] = b; pixels[i + 3] = 255; }
                tex.LoadRawTextureData(pixels);
                tex.Apply();
                File.WriteAllBytes(prefix + "-" + k + ".png", tex.EncodeToPNG());
            }
            UnityEngine.Object.DestroyImmediate(tex);
            return $"saved {shots.Count} grabs of {w} x {h} at {x0},{y0} (window {r.left},{r.top} to {r.right},{r.bottom})";
        }

        /// <summary>"title|x|y": a left click at a point of the window's inside, in pixels.</summary>
        public static string Click(string argument)
        {
            var parts = argument.Split('|');
            IntPtr hWnd = FindWindowW(null, parts[0]);
            if (hWnd == IntPtr.Zero) throw new ArgumentException("No window called " + parts[0]);
            int x = int.Parse(parts[1]), y = int.Parse(parts[2]);
            var at = new IntPtr((y << 16) | (x & 0xFFFF));
            PostMessageW(hWnd, 0x0200, IntPtr.Zero, at);              // WM_MOUSEMOVE
            PostMessageW(hWnd, 0x0201, new IntPtr(1), at);            // WM_LBUTTONDOWN
            PostMessageW(hWnd, 0x0202, IntPtr.Zero, at);              // WM_LBUTTONUP
            return $"clicked {x},{y}";
        }

        const uint PW_RENDERFULLCONTENT = 2;

        [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFO
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
            public int colors;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindowW(string cls, string title);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr hWnd, IntPtr dc, uint flags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr dc);
        [DllImport("user32.dll")] static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFO info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dc, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
    }
}
