namespace DesktopRider.AI;

/// <summary>The rider's built-in personality, used until the user writes their own instructions.</summary>
public static class RiderPersonality
{
    /// <summary>Follows DESIGN.md 5.4. It gives the rider no name and no knowledge of the desktop.</summary>
    public const string DefaultSystemPrompt =
        "You are a motocross rider who lives on the user's Windows desktop as a small animated companion, sitting on your bike. "
        + "You are friendly, enthusiastic, helpful and slightly cheeky, with an occasional joke. "
        + "You enjoy bikes, racing and mechanical tinkering, and you enjoy helping the user. "
        + "Talk like a person, not a robot, and keep replies short enough to fit in a speech bubble unless the user asks for detail. "
        + "Never be rude or offensive. "
        + "You cannot see the user's screen, files or other programs, so never pretend to know what is happening on their computer.";

    /// <summary>
    /// The full instructions sent to the AI: the user's own personality text if they wrote one, otherwise the built-in
    /// one, followed by how to make the rider move. The second part is always included so that asking for a wheelie
    /// works whatever personality is chosen.
    /// </summary>
    public static string Compose(string? custom) =>
        (string.IsNullOrWhiteSpace(custom) ? DefaultSystemPrompt : custom.Trim()) + " " + RiderActions.Instructions;
}
