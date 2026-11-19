#nullable enable

using System;


namespace DesktopRider.Core.Configuration
{
    /// <summary>
    /// Everything the user can configure: Appearance, Behaviour and Application. Version 2 has no chat or AI (they
    /// carry on in version 3); a settings file from a version that had them still loads, and their parts are ignored.
    /// </summary>
    public sealed class AppSettings
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public AppearanceSettings Appearance { get; set; } = new();

        public BehaviourSettings Behaviour { get; set; } = new();

        public ApplicationSettings Application { get; set; } = new();
    }

    public sealed class AppearanceSettings
    {
        // Size is shared by every rider. Top end set by Boss (19 November 2026) where his slider sat, about a third of the
        // way past normal; the bottom end is unchanged.
        public const double MinScale = 0.5;
        public const double MaxScale = 1.3;

        /// <summary>The screen width (in pixels at 100% Windows scaling) that <see cref="MaxScale"/> is meant for.</summary>
        public const double MaxScaleScreenWidth = 1920;

        /// <summary>
        /// The biggest size that suits a screen this wide (pixels at 100% Windows scaling): <see cref="MaxScale"/> on a
        /// 1920-pixel screen or wider, and less in step on a narrower one (Boss, 19 November 2026: "a smaller screen's max
        /// can't be 1.3"), never below <see cref="MinScale"/>. Keeps the sidecar's longest trick on the screen.
        /// </summary>
        public static double MaxScaleFor(double screenWidth) =>
            double.IsFinite(screenWidth) && screenWidth > 0
                ? Math.Max(MinScale, Math.Min(MaxScale, MaxScale * screenWidth / MaxScaleScreenWidth))
                : MaxScale;

        /// <summary>The size actually drawn: the chosen <paramref name="scale"/>, but no bigger than suits the screen.</summary>
        public static double ScaleFor(double scale, double screenWidth) => Math.Min(scale, MaxScaleFor(screenWidth));
        public const double DefaultScale = 1.0;
        public const double MinOpacity = 0.2;
        public const double MaxOpacity = 1.0;
        public const double MinAnimationSpeed = 0.5;
        public const double MaxAnimationSpeed = 2.0;

        /// <summary>Character size, where 1.0 is the normal size.</summary>
        public double Scale { get; set; } = DefaultScale;

        public double Opacity { get; set; } = 1.0;

        public double AnimationSpeed { get; set; } = 1.0;

        /// <summary>Who rides about the desktop: Harvey on his bike, the sidecar crew (Sidecar 41) or Louie on his electric bike.</summary>
        public RiderCharacter Character { get; set; } = RiderCharacter.Harvey;

        /// <summary>
        /// Everyone out riding at once, in button order, always including <see cref="Character"/> (who rides in the main
        /// window); the others ride in windows of their own. See <see cref="RiderLineUp"/>.
        /// </summary>
        public System.Collections.Generic.List<RiderCharacter> Riders { get; set; } = new();

        public bool AlwaysOnTop { get; set; } = true;

        public bool CharacterVisible { get; set; } = true;

        /// <summary>Lets clicks pass through the character to the windows behind it.</summary>
        public bool ClickThrough { get; set; }

        /// <summary>Last saved position in screen coordinates, or null to use the default spot.</summary>
        public SavedPosition? Position { get; set; }
    }

    /// <summary>Where the character was left, measured in physical screen pixels from the top-left of the main screen.</summary>
    public sealed class SavedPosition
    {
        public double X { get; set; }

        public double Y { get; set; }

        /// <summary>Device name of the monitor the character was on, used to restore it to the same screen.</summary>
        public string? Monitor { get; set; }
    }

    public sealed class BehaviourSettings
    {
        public bool AutonomousMovement { get; set; } = true;

        public bool IdleAnimations { get; set; } = true;

        /// <summary>
        /// Whether the rider rides freely around the whole screen by itself. Off until the user switches it on.
        /// New in version 2; version 1 ignores it, and a version 1 settings file simply leaves it off.
        /// </summary>
        public bool FreeRiding { get; set; }

        public AnimationFrequency AnimationFrequency { get; set; } = AnimationFrequency.Normal;

        /// <summary>
        /// How often the rider does a trick without being asked (Boss's choice, 14 November 2026). New in version 2;
        /// a settings file without it starts at Sometimes.
        /// </summary>
        public TrickFrequency TricksByHimself { get; set; } = TrickFrequency.Sometimes;

        public const double MinRidingSpeed = 0.5;
        public const double MaxRidingSpeed = 2.0;

        /// <summary>How fast the rider rides about the screens, where 1 is his own natural speed (Boss, 17 November 2026).</summary>
        public double RidingSpeed { get; set; } = 1.0;

    }

    /// <summary>How often the rider does a trick by himself.</summary>
    /// <summary>The characters that can ride about. Saved as a number: add new ones at the end.</summary>
    public enum RiderCharacter
    {
        Harvey,
        Sidecar41,
        Louie,
    }

    public enum TrickFrequency
    {
        Never,
        Sometimes,
        Often,
    }

    public enum AnimationFrequency
    {
        Rare,
        Normal,
        Frequent,
    }

    public sealed class ApplicationSettings
    {
        public bool StartWithWindows { get; set; }

        public bool MinimiseToTray { get; set; } = true;

        public bool CheckForUpdates { get; set; }

        /// <summary>Set once the user has been told where to find the rider after hiding it, so they are only told once.</summary>
        public bool TrayHintShown { get; set; }
    }
}
