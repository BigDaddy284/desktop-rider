namespace DesktopRider.Core.Configuration;

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
        settings.Ai ??= new AiSettings();
        settings.Memory ??= new MemorySettings();
        settings.Application ??= new ApplicationSettings();

        var appearance = settings.Appearance;
        appearance.Scale = Clamp(appearance.Scale, AppearanceSettings.MinScale, AppearanceSettings.MaxScale, 1.0);
        appearance.Opacity = Clamp(appearance.Opacity, AppearanceSettings.MinOpacity, AppearanceSettings.MaxOpacity, 1.0);
        appearance.AnimationSpeed = Clamp(appearance.AnimationSpeed, AppearanceSettings.MinAnimationSpeed, AppearanceSettings.MaxAnimationSpeed, 1.0);
        if (appearance.Position is { } position && !(double.IsFinite(position.X) && double.IsFinite(position.Y)))
        {
            appearance.Position = null;
        }

        var behaviour = settings.Behaviour;
        behaviour.ProactiveIntervalMinutes = Math.Clamp(
            behaviour.ProactiveIntervalMinutes,
            BehaviourSettings.MinProactiveIntervalMinutes,
            BehaviourSettings.MaxProactiveIntervalMinutes);
        if (!Enum.IsDefined(behaviour.AnimationFrequency))
        {
            behaviour.AnimationFrequency = AnimationFrequency.Normal;
        }

        var ai = settings.Ai;
        if (string.IsNullOrWhiteSpace(ai.Provider))
        {
            ai.Provider = AiSettings.OllamaProvider;
        }

        ai.OllamaAddress = NormalizeAddress(ai.OllamaAddress);
        ai.Model = string.IsNullOrWhiteSpace(ai.Model) ? null : ai.Model.Trim();
        ai.SystemPrompt = Tidy(ai.SystemPrompt, AiSettings.MaxSystemPromptLength);
        ai.UserName = Tidy(ai.UserName, AiSettings.MaxUserNameLength);
        if (!Enum.IsDefined(ai.Tone))
        {
            ai.Tone = RiderTone.Cheeky;
        }

        if (!Enum.IsDefined(ai.ReplyLength))
        {
            ai.ReplyLength = ReplyLength.Brief;
        }

        ai.Temperature = Clamp(ai.Temperature, AiSettings.MinTemperature, AiSettings.MaxTemperature, 0.8);
        ai.RequestTimeoutSeconds = Math.Clamp(ai.RequestTimeoutSeconds, AiSettings.MinTimeoutSeconds, AiSettings.MaxTimeoutSeconds);

        settings.Memory.RetentionDays = Math.Clamp(
            settings.Memory.RetentionDays,
            MemorySettings.MinRetentionDays,
            MemorySettings.MaxRetentionDays);

        return settings;
    }

    /// <summary>Returns a usable http/https address, falling back to the standard local Ollama address.</summary>
    public static string NormalizeAddress(string? address)
    {
        var trimmed = address?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(trimmed)
            && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return trimmed;
        }

        return AiSettings.DefaultOllamaAddress;
    }

    // Text the user typed: no stray spaces, never longer than allowed, and null when there is nothing there.
    private static string? Tidy(string? text, int maxLength)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length > maxLength ? trimmed[..maxLength].TrimEnd() : trimmed;
    }

    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
