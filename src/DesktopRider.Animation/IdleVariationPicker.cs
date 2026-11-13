namespace DesktopRider.Animation;

/// <summary>
/// Now and then asks for a small idle animation so the rider does not look frozen. It only acts while the rider is resting,
/// and does nothing at all when idle animations are switched off.
/// </summary>
public sealed class IdleVariationPicker
{
    private readonly AnimationPlayer _player;
    private readonly IReadOnlyList<string> _choices;
    private readonly Random _random;
    private TimeSpan _untilNext;

    public IdleVariationPicker(AnimationPlayer player, IEnumerable<string> choices, Random? random = null)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _choices = [.. choices ?? throw new ArgumentNullException(nameof(choices))];
        _random = random ?? Random.Shared;
        _untilNext = NextGap();
    }

    public bool Enabled { get; set; } = true;

    /// <summary>Shortest and longest wait between idle animations.</summary>
    public TimeSpan MinGap { get; set; } = TimeSpan.FromSeconds(8);

    public TimeSpan MaxGap { get; set; } = TimeSpan.FromSeconds(20);

    public void Advance(TimeSpan elapsed)
    {
        if (!Enabled || _choices.Count == 0 || !_player.IsResting)
        {
            return;
        }

        _untilNext -= elapsed;
        if (_untilNext > TimeSpan.Zero)
        {
            return;
        }

        _player.Request(_choices[_random.Next(_choices.Count)]);
        _untilNext = NextGap();
    }

    private TimeSpan NextGap()
    {
        var min = MinGap < TimeSpan.Zero ? TimeSpan.Zero : MinGap;
        var max = MaxGap < min ? min : MaxGap;
        return min + ((max - min) * _random.NextDouble());
    }
}
