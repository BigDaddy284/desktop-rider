namespace DesktopRider.Animation;

/// <summary>
/// How far the character's window moves while it rides about. The riding animations play on the spot, so the window
/// itself is moved: it speeds up through the ride-off, holds its speed in the riding loop and slows to a stop in the
/// ride-on. It draws nothing and has no timer, so it is unit tested.
/// </summary>
public static class RidingMotion
{
    public const string RideOff = "RideOff";
    public const string RidingLoop = "RidingLoop";
    public const string RideOn = "RideOn";

    /// <summary>Slowest and fastest choice for the user's riding speed, where 1.0 is the character's natural speed.</summary>
    public const double MinSpeed = 0.25;
    public const double MaxSpeed = 2.0;

    /// <summary>True for the animations in which the character is riding along.</summary>
    public static bool IsRiding(string animation) =>
        string.Equals(animation, RideOff, StringComparison.OrdinalIgnoreCase)
        || string.Equals(animation, RidingLoop, StringComparison.OrdinalIgnoreCase)
        || string.Equals(animation, RideOn, StringComparison.OrdinalIgnoreCase);

    /// <summary>The share of full riding speed at a point in an animation, from 0 to 1.</summary>
    /// <param name="animation">The animation showing.</param>
    /// <param name="progress">How far through it the character is, from 0 at the start to 1 at the end.</param>
    public static double SpeedShare(string animation, double progress)
    {
        var p = Math.Clamp(double.IsFinite(progress) ? progress : 0, 0, 1);
        if (string.Equals(animation, RideOff, StringComparison.OrdinalIgnoreCase))
        {
            return p;
        }

        if (string.Equals(animation, RideOn, StringComparison.OrdinalIgnoreCase))
        {
            return 1 - p;
        }

        return string.Equals(animation, RidingLoop, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }

    /// <summary>Full riding speed on screen, in screen pixels a second.</summary>
    /// <param name="manifest">The character's sprite list, which gives its riding speed and scale.</param>
    /// <param name="displayScale">How big a sprite pixel is drawn on screen.</param>
    /// <param name="userSpeed">The user's riding speed choice, where 1.0 is the character's natural speed.</param>
    public static double PixelsPerSecond(SpriteManifest manifest, double displayScale, double userSpeed)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.RideSpeed <= 0 || manifest.PixelsPerMetre <= 0 || !(displayScale > 0) || !double.IsFinite(displayScale))
        {
            return 0;
        }

        return manifest.RideSpeed * manifest.PixelsPerMetre * displayScale * ClampSpeed(userSpeed);
    }

    /// <summary>Keeps a riding speed choice within <see cref="MinSpeed"/> and <see cref="MaxSpeed"/>; anything unusable becomes 1.0.</summary>
    public static double ClampSpeed(double userSpeed) => double.IsFinite(userSpeed) ? Math.Clamp(userSpeed, MinSpeed, MaxSpeed) : 1.0;
}
