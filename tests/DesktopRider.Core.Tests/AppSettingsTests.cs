using DesktopRider.Core.Configuration;

namespace DesktopRider.Core.Tests;

public class AppSettingsTests
{
    [Fact]
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

    [Fact]
    public void Normalize_Null_ReturnsDefaults()
    {
        var settings = SettingsNormalizer.Normalize(null);

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.NotNull(settings.Appearance);
        Assert.NotNull(settings.Ai);
    }

    [Fact]
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

    [Theory]
    [InlineData(0.01, AppearanceSettings.MinScale)]
    [InlineData(99, AppearanceSettings.MaxScale)]
    [InlineData(1.25, 1.25)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void Normalize_KeepsScaleInRange(double input, double expected)
    {
        var settings = new AppSettings();
        settings.Appearance.Scale = input;

        Assert.Equal(expected, SettingsNormalizer.Normalize(settings).Appearance.Scale);
    }

    [Fact]
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

    [Fact]
    public void Normalize_DropsAnUnusableSavedPosition()
    {
        var settings = new AppSettings();
        settings.Appearance.Position = new SavedPosition { X = double.NaN, Y = 10 };

        Assert.Null(SettingsNormalizer.Normalize(settings).Appearance.Position);
    }

    [Fact]
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

    [Fact]
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

    [Theory]
    [InlineData(null, AiSettings.DefaultOllamaAddress)]
    [InlineData("", AiSettings.DefaultOllamaAddress)]
    [InlineData("not an address", AiSettings.DefaultOllamaAddress)]
    [InlineData("ftp://example.test", AiSettings.DefaultOllamaAddress)]
    [InlineData("localhost:11434", AiSettings.DefaultOllamaAddress)]
    [InlineData("http://localhost:11434/", "http://localhost:11434")]
    [InlineData("  http://192.168.1.20:11434  ", "http://192.168.1.20:11434")]
    [InlineData("https://ollama.home.test", "https://ollama.home.test")]
    public void NormalizeAddress_AcceptsOnlyWebAddresses(string? input, string expected)
    {
        Assert.Equal(expected, SettingsNormalizer.NormalizeAddress(input));
    }
}
