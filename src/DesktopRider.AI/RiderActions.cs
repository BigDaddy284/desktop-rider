using System.Text;
using System.Text.RegularExpressions;

namespace DesktopRider.AI;

/// <summary>What sort of thing an action is, so that "do some tricks" only ever picks tricks.</summary>
public enum RiderActionKind
{
    /// <summary>A jump, a freestyle trick or a stunt: what people mean by "a trick".</summary>
    Trick,

    /// <summary>Something ordinary done with the bike, such as revving it or riding off for a moment.</summary>
    Bike,

    /// <summary>A small gesture or mood, such as celebrating, looking round or having a sleep.</summary>
    Gesture,
}

/// <summary>How many tricks the user asked for without naming any.</summary>
public enum TrickRequest
{
    None,

    /// <summary>"Do a trick".</summary>
    One,

    /// <summary>"Do some tricks".</summary>
    Several,
}

/// <summary>Something the rider can be asked to do in conversation, such as a wheelie.</summary>
/// <param name="Tag">The word the AI writes in square brackets to make the rider do it.</param>
/// <param name="Animation">The name of the animation to play.</param>
/// <param name="Kind">Whether it is a trick, something ordinary on the bike or a gesture.</param>
/// <param name="Description">What it is, in words the AI can understand.</param>
/// <param name="Phrases">Words in the user's own message that ask for it, used when the AI forgets the tag.</param>
public sealed record RiderAction(string Tag, string Animation, RiderActionKind Kind, string Description, IReadOnlyList<string> Phrases);

/// <summary>
/// The things the rider can do when asked in conversation, and the two ways a request is recognised:
/// a tag such as <c>[wheelie]</c> written by the AI in its reply, or plain words in the user's message.
/// Both are needed because small local models do not reliably follow formatting instructions.
/// </summary>
public static partial class RiderActions
{
    public static IReadOnlyList<RiderAction> All { get; } =
    [
        new("wheelie", "Wheelie", RiderActionKind.Trick, "lift the front wheel in a wheelie", ["wheelie", "wheelies", "wheely"]),
        new("celebrate", "Celebration", RiderActionKind.Gesture, "do a victory celebration", ["celebrate", "celebration", "victory dance"]),
        new("rev", "Revving", RiderActionKind.Bike, "rev the engine", ["rev", "revs", "revving", "rev it"]),
        new("sleep", "Sleeping", RiderActionKind.Gesture, "have a short sleep on the bike", ["go to sleep", "have a nap", "take a nap", "have a sleep", "have a rest"]),
        new("ride", "RidingLoop", RiderActionKind.Bike, "ride the bike for a moment", ["go for a ride", "ride around", "ride off", "go for a spin"]),
        new("look-left", "LookLeft", RiderActionKind.Gesture, "look to the left", ["look left", "look to the left", "look to your left"]),
        new("look-right", "LookRight", RiderActionKind.Gesture, "look to the right", ["look right", "look to the right", "look to your right"]),
        new("jump", "Jump", RiderActionKind.Trick, "ride over a dirt jump", ["do a jump", "hit a jump", "hit the jump", "go over a jump", "jump the bike", "big jump", "jump over"]),
        new("whip", "Whip", RiderActionKind.Trick, "throw a whip over a jump, laying the bike flat in the air", ["a whip", "whip it", "whip the bike"]),
        new("backflip", "Backflip", RiderActionKind.Trick, "do a backflip off the freestyle ramp", ["backflip", "backflips", "back flip", "do a flip"]),
        new("superman", "Superman", RiderActionKind.Trick, "do a superman, flying flat out behind the bike", ["superman"]),
        new("seat-grab", "SupermanSeatGrab", RiderActionKind.Trick, "do a superman seat grab, holding on by the seat", ["seat grab", "seatgrab", "superman seat grab"]),
        new("no-hander", "NoHander", RiderActionKind.Trick, "jump with both hands off the bars", ["no hander", "nohander", "no handed", "no hands"]),
        new("no-footer", "NoFooter", RiderActionKind.Trick, "jump with both feet off the pegs", ["no footer", "nofooter", "no feet"]),
        new("nothing", "Nothing", RiderActionKind.Trick, "do a nothing: no hands and no feet in the air", ["do a nothing", "nothing trick", "no hands no feet", "no hands and no feet"]),
        new("heel-clicker", "HeelClicker", RiderActionKind.Trick, "do a heel clicker, clicking the boots together over the bars", ["heel clicker", "heelclicker", "click your heels"]),
        new("can-can", "CanCan", RiderActionKind.Trick, "do a can-can, swinging one leg over the seat", ["can can", "cancan"]),
        new("nac-nac", "NacNac", RiderActionKind.Trick, "do a nac-nac, swinging one leg back over the rear wheel", ["nac nac", "nacnac", "nac"]),
        new("lazy-boy", "LazyBoy", RiderActionKind.Trick, "do a lazy boy, lying back flat on the seat in the air", ["lazy boy", "lazyboy"]),
        new("one-hander", "OneHander", RiderActionKind.Trick, "jump with one hand in the air", ["one hander", "one handed", "one hand"]),
        new("scrub", "Scrub", RiderActionKind.Trick, "scrub a jump, laying the bike flat off the lip to stay low", ["scrub", "scrub it", "scrub the jump"]),
        new("indian-air", "IndianAir", RiderActionKind.Trick, "do an indian air: a superman with the legs scissoring", ["indian air", "indianair"]),
        new("rock-solid", "RockSolid", RiderActionKind.Trick, "do a rock solid, letting go with both hands and floating above the bike", ["rock solid", "rocksolid"]),
        new("whoops", "Whoops", RiderActionKind.Trick, "blitz through a supercross whoop section", ["whoops section", "whoop section", "hit the whoops", "ride the whoops", "through the whoops", "do the whoops", "do some whoops", "skim the whoops", "blitz the whoops"]),
        new("stoppie", "Stoppie", RiderActionKind.Trick, "do a stoppie, lifting the rear wheel on the front brake", ["stoppie", "stoppy", "endo", "nose wheelie"]),
    ];

    /// <summary>The jumps, tricks and stunts: the only things picked when the user asks for "a trick" or "some tricks".</summary>
    public static IReadOnlyList<RiderAction> Tricks { get; } = [.. All.Where(action => action.Kind == RiderActionKind.Trick)];

    /// <summary>
    /// Stands for "one trick, any trick" when the AI writes <c>[trick]</c>. It is not in <see cref="All"/> and has
    /// no animation of its own: whoever reads the tag picks a real trick in its place.
    /// </summary>
    public static RiderAction AnyTrick { get; } = new("trick", string.Empty, RiderActionKind.Trick, "do one trick picked at random", []);

    /// <summary>Stands for "a few different tricks" when the AI writes <c>[tricks]</c>. See <see cref="AnyTrick"/>.</summary>
    public static RiderAction SomeTricks { get; } = new("tricks", string.Empty, RiderActionKind.Trick, "do a few different tricks in a row, picked at random", []);

    /// <summary>
    /// The words that stand in for a trick's tag when the AI wrote the tag in the middle of a sentence
    /// ("fancy a [backflip]?"), so that taking the tag out does not leave a hole.
    /// </summary>
    public static string SpokenName(RiderAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return action == SomeTricks ? "few tricks" : action.Tag.Replace('-', ' ');
    }

    /// <summary>The fewest and the most tricks done for "do some tricks".</summary>
    public const int FewestInARow = 3;

    public const int MostInARow = 4;

    private static readonly string[] OneTrickPhrases = ["another trick", "one more trick", "another stunt", "do something cool", "show off"];

    private static readonly string[] SeveralTricksPhrases =
        ["more tricks", "more stunts", "trick show", "put on a show", "show me what you can do", "what you ve got", "what you got", "what ya got", "what you can do"];

    /// <summary>Added to the rider's personality so the AI knows what it can do and how to do it.</summary>
    public static string Instructions { get; } =
        "You can move. You only move when you write a tag: saying you will do something does not make it happen. "
        + "When the user asks you to do one of these things, or it would be fun, put its tag at the very start of your reply, then answer normally. "
        + "Tricks, jumps and stunts: " + Describe(RiderActionKind.Trick)
        + ". Ordinary things on the bike, which are not tricks: " + Describe(RiderActionKind.Bike)
        + ". Gestures, which are not tricks: " + Describe(RiderActionKind.Gesture)
        + $". When the user asks for tricks without naming any, write [{SomeTricks.Tag}] to {SomeTricks.Description}, or [{AnyTrick.Tag}] to {AnyTrick.Description}. "
        + "Tags go only at the very start of your reply, before any words, never inside a sentence. "
        + "After the tag, when you have been asked to do something, just say a few easy words such as \"Sure, watch this!\" or \"Hold tight!\" "
        + "and do not announce or ask which trick. You may write up to four tags, one after another, to do several things in a row. "
        + "Your thinking and talking movements happen by themselves and are not tricks. "
        + "Use only these tags, exactly as written. The user does not see the tags.";

    private static string Describe(RiderActionKind kind) =>
        string.Join("; ", All.Where(action => action.Kind == kind).Select(action => $"[{action.Tag}] to {action.Description}"));

    private static readonly string[] QuestionStarts = ["what", "why", "how", "when", "who", "which", "where"];

    /// <summary>
    /// The action for a tag the AI wrote, with or without its square brackets. Null if it is not one.
    /// <c>[trick]</c> and <c>[tricks]</c> give <see cref="AnyTrick"/> and <see cref="SomeTricks"/>.
    /// </summary>
    public static RiderAction? FromTag(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var name = tag.Trim().Trim('[', ']').Trim().Replace(' ', '-').Replace('_', '-');
        if (Is(name, "tricks") || Is(name, "stunts") || Is(name, "some-tricks") || Is(name, "random-tricks"))
        {
            return SomeTricks;
        }

        if (Is(name, "trick") || Is(name, "stunt") || Is(name, "random-trick"))
        {
            return AnyTrick;
        }

        return All.FirstOrDefault(action => Is(action.Tag, name));

        static bool Is(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The actions the user's own message asks for. A question about something ("what is a wheelie?")
    /// is not a request to do it. Asking for "a trick" without naming one picks a trick at random, and
    /// asking for "some tricks" picks a few different ones. Only tricks are ever picked this way.
    /// </summary>
    public static IReadOnlyList<RiderAction> FromRequest(string message, Random? random = null)
    {
        var named = Named(message);
        if (named.Count > 0)
        {
            return named;
        }

        return TricksWanted(message) switch
        {
            TrickRequest.One => RandomTricks(1, random),
            TrickRequest.Several => RandomTricks(HowManyInARow(random), random),
            _ => [],
        };
    }

    /// <summary>The actions the user's message asks for by name, in the order they are listed in <see cref="All"/>.</summary>
    public static IReadOnlyList<RiderAction> Named(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var padded = Padded(message);
        return padded is null || IsQuestion(padded) ? [] : [.. Find(padded, All).OrderBy(action => All.ToList().IndexOf(action))];
    }

    /// <summary>Whether the user's message asks for a trick, or several, without it mattering which.</summary>
    public static TrickRequest TricksWanted(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var padded = Padded(message);
        if (padded is null || IsQuestion(padded))
        {
            return TrickRequest.None;
        }

        // "Do you know any tricks?" asks what the rider knows; it is not a request to do them.
        if (Has(padded, "know"))
        {
            return TrickRequest.None;
        }

        return SeveralTricksPhrases.Any(phrase => Has(padded, phrase)) || AskForTricks().IsMatch(padded) ? TrickRequest.Several
            : OneTrickPhrases.Any(phrase => Has(padded, phrase)) || AskForATrick().IsMatch(padded) ? TrickRequest.One
            : TrickRequest.None;
    }

    /// <summary>
    /// The tricks named in something the rider said, in the order it said them. Used when the user asked for tricks
    /// and the AI answered "how about a no-hander" without writing the tag. Only tricks are looked for.
    /// </summary>
    public static IReadOnlyList<RiderAction> Mentioned(string reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        return Padded(reply) is { } padded ? Find(padded, Tricks) : [];
    }

    /// <summary>How many tricks to do for "do some tricks".</summary>
    public static int HowManyInARow(Random? random = null) => (random ?? Random.Shared).Next(FewestInARow, MostInARow + 1);

    /// <summary>Different tricks picked at random, never anything that is not a trick.</summary>
    /// <param name="count">How many are wanted. Fewer come back if there are not that many left to pick.</param>
    /// <param name="random">The source of chance.</param>
    /// <param name="except">Tricks to leave out, such as ones already done.</param>
    public static IReadOnlyList<RiderAction> RandomTricks(int count, Random? random = null, IEnumerable<RiderAction>? except = null)
    {
        var left = except is null ? Tricks.ToList() : [.. Tricks.Except(except)];
        var source = random ?? Random.Shared;
        var picked = new List<RiderAction>();
        while (picked.Count < count && left.Count > 0)
        {
            var at = source.Next(left.Count);
            picked.Add(left[at]);
            left.RemoveAt(at);
        }

        return picked;
    }

    // Lower case, with every run of anything else turned into one space, and a space at each end. Null if nothing is left.
    private static string? Padded(string text)
    {
        var words = WordBreaks().Replace(text.ToLowerInvariant(), " ").Trim();
        return words.Length == 0 ? null : " " + words + " ";
    }

    private static bool IsQuestion(string padded) => QuestionStarts.Any(start => padded.StartsWith(" " + start + " ", StringComparison.Ordinal));

    private static bool Has(string padded, string phrase) => padded.Contains(" " + phrase + " ", StringComparison.Ordinal);

    // The actions whose words appear, in the order they appear, each once.
    private static List<RiderAction> Find(string padded, IReadOnlyList<RiderAction> among)
    {
        var found = new List<(RiderAction Action, int Start, int End)>();
        foreach (var action in among)
        {
            foreach (var phrase in action.Phrases)
            {
                for (var at = padded.IndexOf(" " + phrase + " ", StringComparison.Ordinal); at >= 0; at = padded.IndexOf(" " + phrase + " ", at + 1, StringComparison.Ordinal))
                {
                    found.Add((action, at, at + phrase.Length));
                }
            }
        }

        // "nose wheelie" is a stoppie, not a wheelie: words that are only part of a longer match do not count.
        return [.. found
            .Where(match => !found.Any(other => other.Action != match.Action
                && other.Start <= match.Start && other.End >= match.End
                && other.End - other.Start > match.End - match.Start))
            .OrderBy(match => match.Start)
            .Select(match => match.Action)
            .Distinct()];
    }

    // An asking word, then at most six words, then "tricks": "do some tricks", "show me a couple of your best stunts".
    [GeneratedRegex(@" (do|show|pull|bust|throw|try|perform|see|watch|give)( [a-z0-9]+){0,6} (tricks|stunts) ")]
    private static partial Regex AskForTricks();

    [GeneratedRegex(@" (do|show|pull|bust|throw|try|perform|see|watch|give)( [a-z0-9]+){0,6} (trick|stunt) ")]
    private static partial Regex AskForATrick();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex WordBreaks();
}

/// <summary>
/// Takes action tags such as <c>[wheelie]</c> out of a reply as it arrives piece by piece, so the user never sees
/// them. A tag may be split across pieces. Anything else in square brackets is left in the reply untouched.
/// A trick's tag written in the middle of a sentence ("fancy a [backflip]?") is replaced by the trick's name,
/// so the sentence still reads properly.
/// </summary>
public sealed class ActionTagFilter
{
    /// <summary>Longer than any tag. Brackets holding more than this are ordinary text.</summary>
    private const int MaxTagLength = 24;

    private readonly StringBuilder _held = new();
    private char _lastShown = '\n';

    /// <summary>True once a tag has been taken out of this reply.</summary>
    public bool SeenTag { get; private set; }

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
                    SeenTag = true;
                    var before = LastShown(text);
                    if (action.Kind == RiderActionKind.Trick && (char.IsLetterOrDigit(before) || before == ','))
                    {
                        // Mid-sentence: the words carry on, so the trick's name takes the tag's place.
                        text.Append(RiderActions.SpokenName(action));
                    }
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

        _lastShown = LastShown(text);
        return (text.ToString(), actions ?? (IReadOnlyList<RiderAction>)[]);
    }

    // The last thing shown that is not a space, in this piece or an earlier one. A new line if nothing has been shown.
    private char LastShown(StringBuilder text)
    {
        for (var i = text.Length - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return text[i];
            }
        }

        return _lastShown;
    }

    /// <summary>Call when the reply has ended. Returns anything still held back, which was not a tag after all.</summary>
    public string Finish()
    {
        var rest = _held.ToString();
        _held.Clear();
        return rest;
    }
}
