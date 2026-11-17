#nullable enable

using DesktopRider.Core.Configuration;
using NUnit.Framework;

namespace DesktopRider.Core.Tests
{
    public class AppSettingsTests
    {
        [Test]
        public void Defaults_FollowTheDesignSpecification()
        {
            var settings = new AppSettings();

            Assert.True(settings.Appearance.AlwaysOnTop);
            Assert.True(settings.Appearance.CharacterVisible);
            Assert.False(settings.Appearance.ClickThrough);
            Assert.Null(settings.Appearance.Position);
            Assert.False(settings.Application.StartWithWindows);
            Assert.False(settings.Application.TrayHintShown);
            Assert.False(settings.Application.CheckForUpdates); // No network requests unless the user opts in.
            Assert.Equal(1.0, settings.Appearance.Scale);
        }

        [Test]
        public void Normalize_Null_ReturnsDefaults()
        {
            var settings = SettingsNormalizer.Normalize(null);

            Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
            Assert.NotNull(settings.Appearance);
            Assert.NotNull(settings.Behaviour);
        }

        [Test]
        public void Normalize_ReplacesMissingSections()
        {
            var settings = new AppSettings { Appearance = null!, Behaviour = null!, Application = null! };

            SettingsNormalizer.Normalize(settings);

            Assert.NotNull(settings.Appearance);
            Assert.NotNull(settings.Behaviour);
            Assert.NotNull(settings.Application);
        }
        [TestCase(0.01, AppearanceSettings.MinScale)]
        [TestCase(99, AppearanceSettings.MaxScale)]
        [TestCase(1.25, 1.25)]
        [TestCase(double.NaN, 1.0)]
        [TestCase(double.PositiveInfinity, 1.0)]
        public void Normalize_KeepsScaleInRange(double input, double expected)
        {
            var settings = new AppSettings();
            settings.Appearance.Scale = input;

            Assert.Equal(expected, SettingsNormalizer.Normalize(settings).Appearance.Scale);
        }

        [Test]
        public void Normalize_ClampsOutOfRangeValues()
        {
            var settings = new AppSettings();
            settings.Appearance.Opacity = 0;
            settings.Appearance.AnimationSpeed = 50;
            settings.Behaviour.AnimationFrequency = (AnimationFrequency)42;

            SettingsNormalizer.Normalize(settings);

            Assert.Equal(AppearanceSettings.MinOpacity, settings.Appearance.Opacity);
            Assert.Equal(AppearanceSettings.MaxAnimationSpeed, settings.Appearance.AnimationSpeed);
            Assert.Equal(AnimationFrequency.Normal, settings.Behaviour.AnimationFrequency);
        }

        [Test]
        public void Normalize_DropsAnUnusableSavedPosition()
        {
            var settings = new AppSettings();
            settings.Appearance.Position = new SavedPosition { X = double.NaN, Y = 10 };

            Assert.Null(SettingsNormalizer.Normalize(settings).Appearance.Position);
        }

        [Test]
        public void Normalize_KeepsAValidSavedPosition_IncludingNegativeCoordinates()
        {
            // Monitors to the left of or above the main screen have negative coordinates.
            var settings = new AppSettings();
            settings.Appearance.Position = new SavedPosition { X = -1500, Y = -200, Monitor = @"\\.\DISPLAY2" };

            var position = SettingsNormalizer.Normalize(settings).Appearance.Position;

            Assert.NotNull(position);
            Assert.Equal(-1500, position.X);
            Assert.Equal(@"\\.\DISPLAY2", position.Monitor);
        }

    
        [Test]
        public void Character_and_riding_speed_default_to_Harvey_at_his_own_speed()
        {
            var settings = new AppSettings();
            Assert.Equal(RiderCharacter.Harvey, settings.Appearance.Character);
            Assert.Equal(1.0, settings.Behaviour.RidingSpeed);
        }

        [Test]
        public void Normalize_RepairsAnUnknownCharacterAndAnImpossibleRidingSpeed()
        {
            var settings = new AppSettings();
            settings.Appearance.Character = (RiderCharacter)42;
            settings.Behaviour.RidingSpeed = double.NaN;
            SettingsNormalizer.Normalize(settings);
            Assert.Equal(RiderCharacter.Harvey, settings.Appearance.Character);
            Assert.Equal(1.0, settings.Behaviour.RidingSpeed);

            settings.Behaviour.RidingSpeed = 7;
            settings.Appearance.Character = RiderCharacter.Sidecar41;
            SettingsNormalizer.Normalize(settings);
            Assert.Equal(BehaviourSettings.MaxRidingSpeed, settings.Behaviour.RidingSpeed);
            Assert.Equal(RiderCharacter.Sidecar41, settings.Appearance.Character);
        }
    }
}
