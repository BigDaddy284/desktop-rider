namespace DesktopRider.Core.Configuration;

/// <summary>
/// Everything the user can configure. The sections mirror the settings screen:
/// Appearance, Behaviour, AI, Memory and Application.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public AppearanceSettings Appearance { get; set; } = new();

    public BehaviourSettings Behaviour { get; set; } = new();

    public AiSettings Ai { get; set; } = new();

    public MemorySettings Memory { get; set; } = new();

    public ApplicationSettings Application { get; set; } = new();
}

public sealed class AppearanceSettings
{
    public const double MinScale = 0.5;
    public const double MaxScale = 2.0;
    public const double MinOpacity = 0.2;
    public const double MaxOpacity = 1.0;
    public const double MinAnimationSpeed = 0.5;
    public const double MaxAnimationSpeed = 2.0;

    /// <summary>Character size, where 1.0 is the normal size.</summary>
    public double Scale { get; set; } = 1.0;

    public double Opacity { get; set; } = 1.0;

    public double AnimationSpeed { get; set; } = 1.0;

    public bool AlwaysOnTop { get; set; } = true;

    public bool CharacterVisible { get; set; } = true;

    /// <summary>Lets clicks pass through the character to the windows behind it.</summary>
    public bool ClickThrough { get; set; }

    /// <summary>Last saved position in screen coordinates, or null to use the default spot.</summary>
    public SavedPosition? Position { get; set; }
}

public sealed class SavedPosition
{
    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>Device name of the monitor the character was on, used to restore it to the same screen.</summary>
    public string? Monitor { get; set; }
}

public sealed class BehaviourSettings
{
    public const int MinProactiveIntervalMinutes = 5;
    public const int MaxProactiveIntervalMinutes = 240;

    public bool AutonomousMovement { get; set; } = true;

    public bool IdleAnimations { get; set; } = true;

    public AnimationFrequency AnimationFrequency { get; set; } = AnimationFrequency.Normal;

    /// <summary>The rider only starts conversations when the user has switched this on.</summary>
    public bool ProactiveComments { get; set; }

    public int ProactiveIntervalMinutes { get; set; } = 30;

    public bool QuietHoursEnabled { get; set; }

    public TimeOnly QuietHoursStart { get; set; } = new(22, 0);

    public TimeOnly QuietHoursEnd { get; set; } = new(8, 0);
}

public enum AnimationFrequency
{
    Rare,
    Normal,
    Frequent,
}

public sealed class AiSettings
{
    public const string OllamaProvider = "Ollama";
    public const string DefaultOllamaAddress = "http://localhost:11434";
    public const double MinTemperature = 0.0;
    public const double MaxTemperature = 2.0;
    public const int MinTimeoutSeconds = 5;
    public const int MaxTimeoutSeconds = 600;

    public string Provider { get; set; } = OllamaProvider;

    public string OllamaAddress { get; set; } = DefaultOllamaAddress;

    /// <summary>The model the user picked. Null until they choose one; no model name is ever assumed.</summary>
    public string? Model { get; set; }

    /// <summary>Custom personality instructions. Null means the built-in rider personality is used.</summary>
    public string? SystemPrompt { get; set; }

    public double Temperature { get; set; } = 0.8;

    public int RequestTimeoutSeconds { get; set; } = 120;
}

public sealed class MemorySettings
{
    public const int MinRetentionDays = 1;
    public const int MaxRetentionDays = 365;

    public bool Enabled { get; set; } = true;

    /// <summary>Keep conversations between sessions. Always stored on this computer only.</summary>
    public bool KeepConversationHistory { get; set; } = true;

    public int RetentionDays { get; set; } = 30;
}

public sealed class ApplicationSettings
{
    public bool StartWithWindows { get; set; }

    public bool MinimiseToTray { get; set; } = true;

    public bool CheckForUpdates { get; set; }
}
