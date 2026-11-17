#nullable enable

using System;

namespace DesktopRider.Core.Configuration
{
    /// <summary>
    /// Repairs settings that were edited by hand or written by an older version, so the rest of the
    /// application can rely on every value being present and within range.
    /// </summary>
    public static class SettingsNormalizer
    {
        public static AppSettings Normalize(AppSettings? settings)
        {
            settings ??= new AppSettings();
            settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
            settings.Appearance ??= new AppearanceSettings();
            settings.Behaviour ??= new BehaviourSettings();
            settings.Application ??= new ApplicationSettings();

            var appearance = settings.Appearance;
            appearance.Scale = Clamp(appearance.Scale, AppearanceSettings.MinScale, AppearanceSettings.MaxScale, 1.0);
            appearance.Opacity = Clamp(appearance.Opacity, AppearanceSettings.MinOpacity, AppearanceSettings.MaxOpacity, 1.0);
            appearance.AnimationSpeed = Clamp(appearance.AnimationSpeed, AppearanceSettings.MinAnimationSpeed, AppearanceSettings.MaxAnimationSpeed, 1.0);
            if (!Enum.IsDefined(typeof(RiderCharacter), appearance.Character))
            {
                appearance.Character = RiderCharacter.Harvey;
            }

            if (appearance.Position is { } position && !(double.IsFinite(position.X) && double.IsFinite(position.Y)))
            {
                appearance.Position = null;
            }

            var behaviour = settings.Behaviour;
            if (!Enum.IsDefined(typeof(AnimationFrequency), behaviour.AnimationFrequency))
            {
                behaviour.AnimationFrequency = AnimationFrequency.Normal;
            }

            if (!Enum.IsDefined(typeof(TrickFrequency), behaviour.TricksByHimself))
            {
                behaviour.TricksByHimself = TrickFrequency.Sometimes;
            }

            behaviour.RidingSpeed = Clamp(behaviour.RidingSpeed, BehaviourSettings.MinRidingSpeed, BehaviourSettings.MaxRidingSpeed, 1.0);

            return settings;
        }

        private static double Clamp(double value, double min, double max, double fallback) =>
            double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    }
}
