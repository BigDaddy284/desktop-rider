namespace DesktopRider.AI;

/// <summary>Who said a line of the conversation.</summary>
public enum ChatRole
{
    /// <summary>Instructions that set the rider's personality.</summary>
    System,

    /// <summary>The person using Desktop Rider.</summary>
    User,

    /// <summary>The rider.</summary>
    Assistant,
}

/// <summary>One line of a conversation.</summary>
public sealed record ChatMessage(ChatRole Role, string Content);

/// <summary>Everything a provider needs to produce a reply.</summary>
/// <param name="Model">The model the user chose. Never assumed by the application.</param>
/// <param name="Messages">The conversation so far, oldest first.</param>
/// <param name="Temperature">How adventurous the reply may be, or null for the model's own default.</param>
public sealed record ChatRequest(string Model, IReadOnlyList<ChatMessage> Messages, double? Temperature = null);

/// <summary>A model the provider has available.</summary>
/// <param name="Name">The name to ask for, exactly as the provider reports it.</param>
/// <param name="SizeBytes">Size on disk, when the provider reports it.</param>
/// <param name="Details">A short description such as "8.0B, Q4_K_M", when the provider reports it.</param>
public sealed record AiModelInfo(string Name, long? SizeBytes = null, string? Details = null);
