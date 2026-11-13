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

            Assert.Equal("http://localhost:11434", settings.Ai.OllamaAddress);
            Assert.Equal("Ollama", settings.Ai.Provider);
            Assert.Null(settings.Ai.Model); // No model name is ever assumed.
            Assert.False(settings.Behaviour.ProactiveComments); // Proactive behaviour is off until enabled.
            Assert.True(settings.Appearance.AlwaysOnTop);
            Assert.True(settings.Appearance.CharacterVisible);
            Assert.False(settings.Appearance.ClickThrough);
            Assert.False(settings.Appearance.ChatInBubbles);
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
            Assert.NotNull(settings.Ai);
        }

        [Test]
        public void Normalize_ReplacesMissingSections()
        {
            var settings = new AppSettings { Appearance = null!, Behaviour = null!, Ai = null!, Memory = null!, Application = null! };

            SettingsNormalizer.Normalize(settings);

            Assert.NotNull(settings.Appearance);
            Assert.NotNull(settings.Behaviour);
            Assert.NotNull(settings.Ai);
            Assert.NotNull(settings.Memory);
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
            settings.Ai.Temperature = -3;
            settings.Ai.RequestTimeoutSeconds = 0;
            settings.Behaviour.ProactiveIntervalMinutes = 1;
            settings.Behaviour.AnimationFrequency = (AnimationFrequency)42;
            settings.Memory.RetentionDays = 100_000;

            SettingsNormalizer.Normalize(settings);

            Assert.Equal(AppearanceSettings.MinOpacity, settings.Appearance.Opacity);
            Assert.Equal(AppearanceSettings.MaxAnimationSpeed, settings.Appearance.AnimationSpeed);
            Assert.Equal(AiSettings.MinTemperature, settings.Ai.Temperature);
            Assert.Equal(AiSettings.MinTimeoutSeconds, settings.Ai.RequestTimeoutSeconds);
            Assert.Equal(BehaviourSettings.MinProactiveIntervalMinutes, settings.Behaviour.ProactiveIntervalMinutes);
            Assert.Equal(AnimationFrequency.Normal, settings.Behaviour.AnimationFrequency);
            Assert.Equal(MemorySettings.MaxRetentionDays, settings.Memory.RetentionDays);
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
        public void Normalize_TreatsBlankModelAndPromptAsNotSet()
        {
            var settings = new AppSettings();
            settings.Ai.Model = "   ";
            settings.Ai.SystemPrompt = "";
            settings.Ai.Provider = " ";

            SettingsNormalizer.Normalize(settings);

            Assert.Null(settings.Ai.Model);
            Assert.Null(settings.Ai.SystemPrompt);
            Assert.Equal(AiSettings.OllamaProvider, settings.Ai.Provider);
        }

        [Test]
        public void Defaults_GiveTheStandardRider_WithNoNameForTheUser()
        {
            var ai = new AppSettings().Ai;

            Assert.Equal(RiderTone.Cheeky, ai.Tone);
            Assert.Equal(ReplyLength.Brief, ai.ReplyLength);
            Assert.Null(ai.UserName);
            Assert.Null(ai.SystemPrompt);
        }

        [Test]
        public void Normalize_TidiesThePersonalityChoices()
        {
            var settings = new AppSettings();
            settings.Ai.Tone = (RiderTone)99;
            settings.Ai.ReplyLength = (ReplyLength)(-1);
            settings.Ai.UserName = "  " + new string('x', 60) + "  ";
            settings.Ai.SystemPrompt = "  " + new string('y', 3000);

            SettingsNormalizer.Normalize(settings);

            Assert.Equal(RiderTone.Cheeky, settings.Ai.Tone);
            Assert.Equal(ReplyLength.Brief, settings.Ai.ReplyLength);
            Assert.Equal(new string('x', AiSettings.MaxUserNameLength), settings.Ai.UserName);
            Assert.Equal(new string('y', AiSettings.MaxSystemPromptLength), settings.Ai.SystemPrompt);
        }

        [Test]
        public void Normalize_TreatsABlankNameAsNotSet_AndTrimsARealOne()
        {
            var settings = new AppSettings();
            settings.Ai.UserName = "   ";
            Assert.Null(SettingsNormalizer.Normalize(settings).Ai.UserName);

            settings.Ai.UserName = "  Sam ";
            Assert.Equal("Sam", SettingsNormalizer.Normalize(settings).Ai.UserName);
        }
        [TestCase(null, AiSettings.DefaultOllamaAddress)]
        [TestCase("", AiSettings.DefaultOllamaAddress)]
        [TestCase("not an address", AiSettings.DefaultOllamaAddress)]
        [TestCase("ftp://example.test", AiSettings.DefaultOllamaAddress)]
        [TestCase("localhost:11434", AiSettings.DefaultOllamaAddress)]
        [TestCase("http://localhost:11434/", "http://localhost:11434")]
        [TestCase("  http://192.168.1.20:11434  ", "http://192.168.1.20:11434")]
        [TestCase("https://ollama.home.test", "https://ollama.home.test")]
        public void NormalizeAddress_AcceptsOnlyWebAddresses(string? input, string expected)
        {
            Assert.Equal(expected, SettingsNormalizer.NormalizeAddress(input));
        }
    }
}
