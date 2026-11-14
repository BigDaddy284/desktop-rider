using UnityEngine;

namespace DesktopRider.Riding
{
    /// <summary>
    /// The isometric view of the desktop as one fixed camera would see it: maps points on the ground (y = 0) to desktop
    /// pixels and back. Desktop pixels are measured from the top-left corner, x right and y down, as Windows does.
    /// The desktop's centre looks at the world origin. Pure arithmetic, so it is unit tested.
    /// </summary>
    public readonly struct DesktopView
    {
        public readonly float Width, Height, PixelsPerMetre;
        public readonly Quaternion Rotation;
        public readonly Vector3 CameraPosition;

        public DesktopView(float width, float height, float pixelsPerMetre, float pitchDegrees, float cameraDistance = 60f)
        {
            Width = width;
            Height = height;
            PixelsPerMetre = pixelsPerMetre;
            Rotation = Quaternion.Euler(pitchDegrees, 0f, 0f);
            CameraPosition = Rotation * new Vector3(0f, 0f, -cameraDistance);
        }

        public Vector3 Right => Rotation * Vector3.right;
        public Vector3 Up => Rotation * Vector3.up;
        public Vector3 Forward => Rotation * Vector3.forward;
        public Vector2 Centre => new Vector2(Width * 0.5f, Height * 0.5f);

        /// <summary>The desktop pixel a world point appears at.</summary>
        public Vector2 ToDesktop(Vector3 world)
        {
            Vector3 relative = world - CameraPosition;
            return new Vector2(Width * 0.5f + Vector3.Dot(relative, Right) * PixelsPerMetre,
                               Height * 0.5f - Vector3.Dot(relative, Up) * PixelsPerMetre);
        }

        /// <summary>The point on the ground under a desktop pixel. False only if the camera looks level with the ground.</summary>
        public bool ToGround(Vector2 pixel, out Vector3 ground)
        {
            Vector3 onPlane = CameraFor(pixel);
            Vector3 forward = Forward;
            if (Mathf.Abs(forward.y) < 1e-5f) { ground = default; return false; }
            float t = -onPlane.y / forward.y;
            ground = onPlane + forward * t;
            ground.y = 0f;
            return true;
        }

        /// <summary>
        /// Where to put a camera with the same rotation so that the centre of its picture is the given desktop pixel.
        /// A small window centred on that pixel then shows exactly what the whole-desktop view shows there.
        /// </summary>
        public Vector3 CameraFor(Vector2 pixel)
        {
            float right = (pixel.x - Width * 0.5f) / PixelsPerMetre;
            float up = (Height * 0.5f - pixel.y) / PixelsPerMetre;
            return CameraPosition + Right * right + Up * up;
        }

        /// <summary>The top-left corner for a window of the given size centred on a pixel, kept fully on the desktop.</summary>
        public Vector2Int WindowCorner(Vector2 centre, int windowWidth, int windowHeight)
        {
            int x = Mathf.RoundToInt(centre.x - windowWidth * 0.5f);
            int y = Mathf.RoundToInt(centre.y - windowHeight * 0.5f);
            x = Mathf.Clamp(x, 0, Mathf.Max(0, (int)Width - windowWidth));
            y = Mathf.Clamp(y, 0, Mathf.Max(0, (int)Height - windowHeight));
            return new Vector2Int(x, y);
        }
    }
}
