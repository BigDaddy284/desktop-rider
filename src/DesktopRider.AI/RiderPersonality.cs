using System.Text;
using DesktopRider.Core.Configuration;

namespace DesktopRider.AI;

/// <summary>
/// The instructions that give the rider its personality. They are put together from the user's choices:
/// how the rider talks, how much it says, what it calls the user and anything it has been asked to remember.
/// </summary>
public static class RiderPersonality
{
    /// <summary>Always included, whatever personality is chosen (DESIGN.md section 10).</summary>
    public const string Honesty =
        "You cannot see the user's screen, files or other programs, so never pretend to know what is happening on their computer.";

    public const string ShortReplies =
        "Keep replies short enough to fit in a speech bubble, a sentence or two, unless the user asks for detail.";

    public const string DetailedReplies =
        "Give full, clear answers and explain your thinking when it helps, without rambling.";

    /// <summary>Tells the AI how to start a conversation by itself. Sent only when the user has switched that on.</summary>
    public const string OwnCommentRequest =
        "(The user has not said anything for a while. Say one short, friendly thing of your own to them: a motocross remark, "
        + "a handy tip, or ask whether they would like a hand with anything. One or two sentences at most. "
        + "You may start with one action tag if a trick or a gesture suits what you say. "
        + "Do not mention this note and do not repeat anything you have said before.)";

    private const string Who =
        "You are a motocross rider who lives on the user's Windows desktop as a small animated companion, sitting on your bike. ";

    private const string Likes =
        "You enjoy bikes, racing and mechanical tinkering, and you enjoy helping the user. ";

    /// <summary>The standard rider, following DESIGN.md 5.4. It gives the rider no name and no knowledge of the desktop.</summary>
    public static string DefaultSystemPrompt { get; } = Describe(RiderTone.Cheeky) + " " + Honesty;

    /// <summary>The built-in description of the rider for each way of talking.</summary>
    public static string Describe(RiderTone tone) => tone switch
    {
        RiderTone.Friendly =>
            Who + "You are friendly, warm, enthusiastic and helpful. " + Likes
            + "Talk like a person, not a robot. Never be rude or offensive.",
        RiderTone.Serious =>
            Who + "You are calm, polite, helpful and to the point, and you do not make jokes. " + Likes
            + "Talk like a person, not a robot. Never be rude or offensive.",
        _ =>
            Who + "You are friendly, enthusiastic, helpful and slightly cheeky, with an occasional joke. " + Likes
            + "Talk like a person, not a robot. Never be rude or offensive.",
    };

    /// <summary>
    /// The full instructions sent to the AI. The user's own description of the rider replaces the built-in one if
    /// they wrote one. The rest is always included, so that the rider stays honest about what it can see and
    /// asking for a wheelie works whatever personality is chosen.
    /// </summary>
    /// <param name="ai">The user's choices.</param>
    /// <param name="memories">Things the user asked the rider to remember. Leave empty when memory is switched off.</param>
    public static string Compose(AiSettings ai, IReadOnlyList<string>? memories = null)
    {
        ArgumentNullException.ThrowIfNull(ai);
        var text = new StringBuilder();
        text.Append(string.IsNullOrWhiteSpace(ai.SystemPrompt) ? Describe(ai.Tone) : ai.SystemPrompt.Trim());
        text.Append(' ').Append(Honesty);
        text.Append(' ').Append(ai.ReplyLength == ReplyLength.Detailed ? DetailedReplies : ShortReplies);

        if (!string.IsNullOrWhiteSpace(ai.UserName))
        {
            text.Append(" The user's name is ").Append(OneLine(ai.UserName)).Append(". Use it now and then, not in every reply.");
        }

        if (memories is { Count: > 0 })
        {
            text.Append(" The user has asked you to remember the following about them. Use it when it is relevant, and do not recite it unless asked:");
            foreach (var memory in memories)
            {
                text.Append(" - ").Append(OneLine(memory)).Append(';');
            }

            text.Length--;
            text.Append('.');
        }

        text.Append(' ').Append(RiderActions.Instructions);
        return text.ToString();
    }

    // Keeps something the user typed on one line, so it cannot break up the instructions around it.
    private static string OneLine(string text) =>
        string.Join(' ', text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).TrimEnd('.', ';', ' ');
}
