using System;
using System.Runtime.InteropServices;
using DesktopRider.Core.Imaging;
using UnityEngine;

namespace DesktopRider.Overlay
{
    /// <summary>
    /// Makes the notification-area icon by drawing the rider's own 3D model once, side on, at start-up, so no separate
    /// picture has to be invented or stored. Returns a Windows icon handle, or zero if anything goes wrong (the caller then
    /// uses the standard application icon).
    /// </summary>
    public static class RiderIcon
    {
        const int RenderSize = 256;

        /// <param name="model">The rider and bike to draw.</param>
        /// <param name="iconSize">Width and height of the icon in pixels; twice the small-icon size keeps it sharp when scaled.</param>
        public static IntPtr Create(Transform model, int iconSize = 48)
        {
            if (model == null) return IntPtr.Zero;
            var pixels = Render(model);
            if (pixels == null) return IntPtr.Zero;
            var icon = Shrink(pixels, RenderSize, iconSize);
            return icon == null ? IntPtr.Zero : ToWindowsIcon(icon, iconSize);
        }

        /// <summary>Draws the model side on with a see-through background. Returns BGRA bytes, top row first.</summary>
        static byte[] Render(Transform model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);

            // Draw only the model: move it to a spare layer for the moment, so the shadow and anything else stay out.
            const int IconLayer = 31;
            var layers = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) { layers[i] = renderers[i].gameObject.layer; renderers[i].gameObject.layer = IconLayer; }

            var go = new GameObject("RiderIconCamera") { hideFlags = HideFlags.HideAndDontSave };
            var target = RenderTexture.GetTemporary(RenderSize, RenderSize, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.cullingMask = 1 << IconLayer;
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.z) * 1.1f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                // Look from the bike's right-hand side, slightly from above, at the middle of the model.
                var side = model.right;
                go.transform.position = bounds.center + side * 10f + Vector3.up * 1.5f;
                go.transform.LookAt(bounds.center);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 30f;
                cam.targetTexture = target;
                cam.Render();
                cam.targetTexture = null;

                RenderTexture.active = target;
                var read = new Texture2D(RenderSize, RenderSize, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, RenderSize, RenderSize), 0, 0);
                read.Apply();
                var rgba = read.GetPixels32();
                UnityEngine.Object.Destroy(read);

                // Unity rows start at the bottom; Windows and IconLayout want the top row first, in BGRA order.
                var bgra = new byte[RenderSize * RenderSize * 4];
                for (int y = 0; y < RenderSize; y++)
                {
                    for (int x = 0; x < RenderSize; x++)
                    {
                        var c = rgba[(RenderSize - 1 - y) * RenderSize + x];
                        int i = (y * RenderSize + x) * 4;
                        bgra[i] = c.b; bgra[i + 1] = c.g; bgra[i + 2] = c.r; bgra[i + 3] = c.a;
                    }
                }
                return bgra;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                for (int i = 0; i < renderers.Length; i++) renderers[i].gameObject.layer = layers[i];
                UnityEngine.Object.Destroy(go);
            }
        }

        /// <summary>Crops to the drawn part and shrinks it into a square icon, averaging pixels so it stays smooth.</summary>
        static byte[] Shrink(byte[] source, int size, int iconSize)
        {
            int stride = size * 4;
            if (!(IconLayout.OpaqueBounds(source, size, size, stride) is { } bounds)) return null;
            var place = IconLayout.FitInSquare(bounds.Width, bounds.Height, iconSize);
            var result = new byte[iconSize * iconSize * 4];
            for (int y = 0; y < place.Height; y++)
            {
                int sy0 = bounds.Y + y * bounds.Height / place.Height, sy1 = Math.Max(sy0 + 1, bounds.Y + (y + 1) * bounds.Height / place.Height);
                for (int x = 0; x < place.Width; x++)
                {
                    int sx0 = bounds.X + x * bounds.Width / place.Width, sx1 = Math.Max(sx0 + 1, bounds.X + (x + 1) * bounds.Width / place.Width);
                    // Colours are weighted by how solid each pixel is, so see-through edges do not darken the picture.
                    long b = 0, g = 0, r = 0, a = 0; int n = 0;
                    for (int sy = sy0; sy < sy1; sy++)
                        for (int sx = sx0; sx < sx1; sx++)
                        {
                            int i = sy * stride + sx * 4;
                            int alpha = source[i + 3];
                            b += source[i] * alpha; g += source[i + 1] * alpha; r += source[i + 2] * alpha; a += alpha; n++;
                        }
                    int o = ((place.Y + y) * iconSize + place.X + x) * 4;
                    if (a > 0) { result[o] = (byte)(b / a); result[o + 1] = (byte)(g / a); result[o + 2] = (byte)(r / a); }
                    result[o + 3] = (byte)(a / n);
                }
            }
            return result;
        }

        static IntPtr ToWindowsIcon(byte[] bgra, int size)
        {
            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = size,
                biHeight = -size,          // negative: top row first
                biPlanes = 1,
                biBitCount = 32,
            };
            IntPtr colour = CreateDIBSection(IntPtr.Zero, ref header, 0, out IntPtr bits, IntPtr.Zero, 0);
            if (colour == IntPtr.Zero) return IntPtr.Zero;
            IntPtr mask = CreateBitmap(size, size, 1, 1, IntPtr.Zero);
            try
            {
                Marshal.Copy(bgra, 0, bits, bgra.Length);
                var info = new ICONINFO { fIcon = true, hbmMask = mask, hbmColor = colour };
                return CreateIconIndirect(ref info);
            }
            finally
            {
                // The icon keeps its own copies of both pictures.
                DeleteObject(colour);
                if (mask != IntPtr.Zero) DeleteObject(mask);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth, biHeight;
            public ushort biPlanes, biBitCount;
            public uint biCompression, biSizeImage;
            public int biXPelsPerMeter, biYPelsPerMeter;
            public uint biClrUsed, biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ICONINFO
        {
            [MarshalAs(UnmanagedType.Bool)] public bool fIcon;
            public int xHotspot, yHotspot;
            public IntPtr hbmMask, hbmColor;
        }

        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER header, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr CreateBitmap(int width, int height, uint planes, uint bitCount, IntPtr bits);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] static extern IntPtr CreateIconIndirect(ref ICONINFO info);
    }
}
