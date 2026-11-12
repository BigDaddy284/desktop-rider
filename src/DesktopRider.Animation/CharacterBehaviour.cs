namespace DesktopRider.Animation;

/// <summary>What the character is doing, as far as the animation is concerned.</summary>
public enum CharacterState
{
    /// <summary>Resting on the bike. Tricks and small idle movements play as usual.</summary>
    Idle,

    /// <summary>Working out a reply.</summary>
    Thinking,

    /// <summary>Saying something.</summary>
    Talking,
}

/// <summary>
/// Chooses the animation that suits what the character is doing. Other parts of the application only say which
/// state the character is in; this class turns that into requests to the <see cref="AnimationPlayer"/>.
/// It knows nothing about the AI or about windows.
/// </summary>
public sealed class CharacterBehaviour
{
    public const string ThinkingAnimation = "Thinking";
    public const string TalkingAnimation = "Talking";

    /// <summary>Conversation animations outrank small idle movements but not each other.</summary>
    public const int Priority = 1;

    private readonly AnimationPlayer _player;
    private readonly SpriteManifest _manifest;
    private readonly AnimationClip? _thinking;
    private readonly AnimationClip? _talking;
    private CharacterState _state;
    private bool _talkOwed;
    private TimeSpan _talkedFor;

    public CharacterBehaviour(AnimationPlayer player, SpriteManifest manifest)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));

        // A character without these animations simply keeps resting; nothing fails.
        _thinking = manifest.TryGet(ThinkingAnimation, out var thinking) ? thinking : null;
        _talking = manifest.TryGet(TalkingAnimation, out var talking) ? talking : null;
    }

    /// <summary>
    /// The shortest time the talking animation is shown once the character has something to say,
    /// so that a very quick reply is still seen being spoken.
    /// </summary>
    public TimeSpan MinimumTalking { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>What the character is doing. Setting it takes effect on the next <see cref="Advance"/>.</summary>
    public CharacterState State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            if (value == CharacterState.Talking)
            {
                _talkOwed = _talking is not null;
                _talkedFor = TimeSpan.Zero;
            }
            else if (value == CharacterState.Thinking)
            {
                // A new question matters more than finishing the last answer's animation.
                _talkOwed = false;
            }
        }
    }

    /// <summary>Moves time forward and keeps the animation in step with <see cref="State"/>.</summary>
    /// <returns>True if the frame to draw changed.</returns>
    public bool Advance(TimeSpan elapsed)
    {
        var before = (_player.Current, _player.FrameIndex);
        _player.Advance(elapsed);

        if (_talkOwed && _player.Current == _talking && elapsed > TimeSpan.Zero)
        {
            _talkedFor += elapsed;
            _talkOwed = _talkedFor < MinimumTalking;
        }

        switch (_state)
        {
            case CharacterState.Thinking:
                Think();
                break;
            case CharacterState.Talking:
                Talk();
                break;
            default:
                if (_talkOwed)
                {
                    Talk();
                }
                else
                {
                    Rest();
                }

                break;
        }

        return before != (_player.Current, _player.FrameIndex);
    }

    // The thinking gesture is a single movement, so it is asked for again each time the character settles.
    private void Think()
    {
        if (_thinking is not null && _player.Current.Loop)
        {
            _player.Request(_thinking.Name, Priority);
        }
    }

    // Talking is a loop. If a gesture is still playing, the request waits for it to finish.
    private void Talk()
    {
        if (_talking is not null && _player.Current != _talking)
        {
            _player.Request(_talking.Name, Priority);
        }
    }

    private void Rest()
    {
        if (_talking is not null && _player.Current == _talking)
        {
            _player.Request(_manifest.Default.Name, Priority);
        }
    }
}
