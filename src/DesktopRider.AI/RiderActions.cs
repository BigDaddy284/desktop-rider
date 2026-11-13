using System.Text;
using System.Text.RegularExpressions;

namespace DesktopRider.AI;

/// <summary>Something the rider can be asked to do in conversation, such as a wheelie.</summary>
/// <param name="Tag">The word the AI writes in square brackets to make the rider do it.</param>
/// <param name="Animation">The name of the animation to play.</param>
/// <param name="Description">What it is, in words the AI can understand.</param>
/// <param name="Phrases">Words in the user's own message that ask for it, used when the AI forgets the tag.</param>
public sealed record RiderAction(string Tag, string Animation, string Description, IReadOnlyList<string> Phrases);

/// <summary>
/// The things the rider can do when asked in conversation, and the two ways a request is recognised:
/// a tag such as <c>[wheelie]</c> written by the AI in its reply, or plain words in the user's message.
/// Both are needed because small local models do not reliably follow formatting instructions.
/// </summary>
public static partial class RiderActions
{
    public static IReadOnlyList<RiderAction> All { get; } =
    [
        new("wheelie", "Wheelie", "lift the front wheel in a wheelie", ["wheelie", "wheelies", "wheely"]),
        new("celebrate", "Celebration", "do a victory celebration", ["celebrate", "celebration", "victory dance"]),
        new("rev", "Revving", "rev the engine", ["rev", "revs", "revving", "rev it"]),
        new("sleep", "Sleeping", "have a short sleep on the bike", ["go to sleep", "have a nap", "take a nap", "have a sleep", "have a rest"]),
        new("ride", "RidingLoop", "ride the bike for a moment", ["go for a ride", "ride around", "ride off", "go for a spin"]),
        new("look-left", "LookLeft", "look to the left", ["look left", "look to the left", "look to your left"]),
        new("look-right", "LookRight", "look to the right", ["look right", "look to the right", "look to your right"]),
        new("jump", "Jump", "ride over a dirt jump", ["do a jump", "hit a jump", "hit the jump", "go over a jump", "jump the bike", "big jump", "jump over"]),
        new("whip", "Whip", "throw a whip over a jump, laying the bike flat in the air", ["a whip", "whip it", "whip the bike"]),
        new("backflip", "Backflip", "do a backflip off the freestyle ramp", ["backflip", "backflips", "back flip", "do a flip"]),
        new("superman", "Superman", "do a superman, flying flat out behind the bike", ["superman"]),
        new("seat-grab", "SupermanSeatGrab", "do a superman seat grab, holding on by the seat", ["seat grab", "seatgrab", "superman seat grab"]),
        new("no-hander", "NoHander", "jump with both hands off the bars", ["no hander", "nohander", "no handed", "no hands"]),
        new("no-footer", "NoFooter", "jump with both feet off the pegs", ["no footer", "nofooter", "no feet"]),
        new("nothing", "Nothing", "do a nothing: no hands and no feet in the air", ["do a nothing", "nothing trick", "no hands no feet", "no hands and no feet"]),
        new("heel-clicker", "HeelClicker", "do a heel clicker, clicking the boots together over the bars", ["heel clicker", "heelclicker", "click your heels"]),
        new("can-can", "CanCan", "do a can-can, swinging one leg over the seat", ["can can", "cancan"]),
        new("nac-nac", "NacNac", "do a nac-nac, swinging one leg back over the rear wheel", ["nac nac", "nacnac", "nac"]),
        new("lazy-boy", "LazyBoy", "do a lazy boy, lying back flat on the seat in the air", ["lazy boy", "lazyboy"]),
        new("one-hander", "OneHander", "jump with one hand in the air", ["one hander", "one handed", "one hand"]),
        new("scrub", "Scrub", "scrub a jump, laying the bike flat off the lip to stay low", ["scrub", "scrub it", "scrub the jump"]),
        new("indian-air", "IndianAir", "do an indian air: a superman with the legs scissoring", ["indian air", "indianair"]),
        new("rock-solid", "RockSolid", "do a rock solid, letting go with both hands and floating above the bike", ["rock solid", "rocksolid"]),
        new("whoops", "Whoops", "blitz through a supercross whoop section", ["whoops section", "whoop section", "hit the whoops", "ride the whoops", "through the whoops", "do the whoops", "do some whoops", "skim the whoops", "blitz the whoops"]),
        new("stoppie", "Stoppie", "do a stoppie, lifting the rear wheel on the front brake", ["stoppie", "stoppy", "endo", "nose wheelie"]),
    ];

    /// <summary>The jumps and tricks, one of which is picked when the user just asks for "a trick".</summary>
    public static IReadOnlyList<RiderAction> Tricks { get; } =
        [.. All.Where(action => action.Tag is not ("rev" or "sleep" or "ride" or "look-left" or "look-right" or "celebrate"))];

    private static readonly string[] AnyTrickPhrases =
        ["do a trick", "do some tricks", "do tricks", "show me a trick", "show me some tricks", "another trick", "do a stunt", "do some stunts", "do something cool", "show off"];

    /// <summary>Added to the rider's personality so the AI knows what it can do and how to do it.</summary>
    public static string Instructions { get; } =
        "You can move. When the user asks you to do one of these things, or it would be fun, put its tag on its own at the very start of your reply, "
        + "then answer normally: "
        + string.Join("; ", All.Select(action => $"[{action.Tag}] to {action.Description}"))
        + ". Use a tag only for these actions, exactly as written, and at most one per reply. The user does not see the tag.";

    private static readonly string[] QuestionStarts = ["what", "why", "how", "when", "who", "which", "where"];

    /// <summary>The action for a tag the AI wrote, with or without its square brackets. Null if it is not one.</summary>
    public static RiderAction? FromTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var name = tag.Trim().Trim('[', ']').Trim().Replace(' ', '-').Replace('_', '-');
        return All.FirstOrDefault(action => string.Equals(action.Tag, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The actions the user's own message asks for. A question about something ("what is a wheelie?")
    /// is not a request to do it. Asking for "a trick" without naming one picks a trick at random.
    /// </summary>
    public static IReadOnlyList<RiderAction> FromRequest(string message, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        var words = WordBreaks().Replace(message.ToLowerInvariant(), " ").Trim();
        if (words.Length == 0 || QuestionStarts.Any(start => words.StartsWith(start + " ", StringComparison.Ordinal)))
        {
            return [];
        }

        var padded = " " + words + " ";
        var found = new List<(RiderAction Action, int Start, int End)>();
        foreach (var action in All)
        {
            foreach (var phrase in action.Phrases)
            {
                var at = padded.IndexOf(" " + phrase + " ", StringComparison.Ordinal);
                if (at >= 0)
                {
                    found.Add((action, at, at + phrase.Length));
                }
            }
        }

        // "nose wheelie" is a stoppie, not a wheelie: words that are only part of a longer match do not count.
        var asked = found
            .Where(match => !found.Any(other => other.Action != match.Action
                && other.Start <= match.Start && other.End >= match.End
                && other.End - other.Start > match.End - match.Start))
            .OrderBy(match => match.Start)
            .Select(match => match.Action)
            .Distinct()
            .OrderBy(action => All.ToList().IndexOf(action))
            .ToList();
        if (asked.Count == 0 && AnyTrickPhrases.Any(phrase => padded.Contains(" " + phrase + " ", StringComparison.Ordinal)))
        {
            asked.Add(Tricks[(random ?? Random.Shared).Next(Tricks.Count)]);
        }

        return asked;
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex WordBreaks();
}

/// <summary>
/// Takes action tags such as <c>[wheelie]</c> out of a reply as it arrives piece by piece, so the user never sees
/// them. A tag may be split across pieces. Anything else in square brackets is left in the reply untouched.
/// </summary>
public sealed class ActionTagFilter
{
    /// <summary>Longer than any tag. Brackets holding more than this are ordinary text.</summary>
    private const int MaxTagLength = 24;

    private readonly StringBuilder _held = new();

    /// <summary>Feeds in the next piece of the reply.</summary>
    /// <returns>The words to show, and any actions whose tags were completed by this piece.</returns>
    public (string Text, IReadOnlyList<RiderAction> Actions) Add(string piece)
    {
        ArgumentNullException.ThrowIfNull(piece);
        var text = new StringBuilder();
        List<RiderAction>? actions = null;
        foreach (var c in piece)
        {
            if (_held.Length == 0)
            {
                if (c == '[')
                {
                    _held.Append(c);
                }
                else
                {
                    text.Append(c);
                }

                continue;
            }

            if (c == '[')
            {
                // A second opening bracket: what was held was not a tag.
                text.Append(_held);
                _held.Clear().Append(c);
                continue;
            }

            _held.Append(c);
            if (c == ']')
            {
                if (RiderActions.FromTag(_held.ToString()) is { } action)
                {
                    (actions ??= []).Add(action);
                }
                else
                {
                    text.Append(_held);
                }

                _held.Clear();
            }
            else if (_held.Length > MaxTagLength || c is '\n' or '\r')
            {
                text.Append(_held);
                _held.Clear();
            }
        }

        return (text.ToString(), actions ?? (IReadOnlyList<RiderAction>)[]);
    }

    /// <summary>Call when the reply has ended. Returns anything still held back, which was not a tag after all.</summary>
    public string Finish()
    {
        var rest = _held.ToString();
        _held.Clear();
        return rest;
    }
}
