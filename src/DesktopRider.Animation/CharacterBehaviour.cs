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

/// <summary>Whether a trick that is next in line can start now.</summary>
public enum TrickReadiness
{
    /// <summary>It can start now.</summary>
    Ready,

    /// <summary>Not yet, for instance because its pictures are still being read. Ask again shortly.</summary>
    Wait,

    /// <summary>It cannot be done at all. Leave it out and move on to the next.</summary>
    Skip,
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
    private bool _talkStarted;
    private TimeSpan _talkedFor;
    private readonly Queue<AnimationClip> _waitingTricks = new();
    private AnimationClip? _trick;
    private bool _trickAsked;
    private bool _trickStarted;
    private TimeSpan _trickLoopedFor;
    private bool _trickShowing;

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

    /// <summary>How long a looping animation such as revving is kept up when it is performed as a trick.</summary>
    public TimeSpan TrickLoopTime { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>The most tricks that can be waiting their turn. More than this are refused.</summary>
    public const int MaxWaitingTricks = 8;

    /// <summary>True while a trick asked for with <see cref="Perform"/> is waiting to start or is playing.</summary>
    public bool IsPerforming => _trick is not null || _waitingTricks.Count > 0;

    /// <summary>
    /// True from the moment the first trick asked for with <see cref="Perform"/> appears until the last one has
    /// finished, including any short rest between two of them. Used to keep the speech bubble out of the way.
    /// </summary>
    public bool IsTrickShowing => _trickShowing;

    /// <summary>
    /// Asked just before a trick starts, so that whoever draws the character can get its pictures ready first.
    /// Left unset, every trick is ready at once.
    /// </summary>
    public Func<AnimationClip, TrickReadiness>? Prepare { get; set; }

    /// <summary>
    /// Asks the character to perform an animation once, such as a wheelie asked for in conversation. It takes
    /// precedence over thinking and talking, which carry on afterwards. A looping animation is kept up for
    /// <see cref="TrickLoopTime"/> and then ended properly. Asked for several, the character does them one
    /// after another in the order they were asked for.
    /// </summary>
    /// <returns>
    /// False if the character has no such animation, it is only a lead-in or lead-out of another, or too many
    /// are already waiting.
    /// </returns>
    public bool Perform(string animation)
    {
        if (!_manifest.TryGet(animation, out var clip)
            || clip == _manifest.Default
            || _manifest.Animations.Any(other => IsNamed(other.Enter, clip) || IsNamed(other.Exit, clip)))
        {
            return false;
        }

        if (_waitingTricks.Count >= MaxWaitingTricks)
        {
            return false;
        }

        _waitingTricks.Enqueue(clip);
        return true;
    }

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

        if (_trick is null)
        {
            TakeNextTrick();
        }

        if (_trick is not null && ContinueTrick(elapsed))
        {
            _trickShowing |= _player.Current == _trick;
            return before != (_player.Current, _player.FrameIndex);
        }

        _trickShowing &= IsPerforming;

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

    private static bool IsNamed(string? name, AnimationClip clip) => string.Equals(name, clip.Name, StringComparison.OrdinalIgnoreCase);

    // Moves on to the next trick in line, if there is one and its pictures are ready.
    private void TakeNextTrick()
    {
        while (_trick is null && _waitingTricks.TryPeek(out var next))
        {
            var readiness = Prepare?.Invoke(next) ?? TrickReadiness.Ready;
            if (readiness == TrickReadiness.Wait)
            {
                return;
            }

            _waitingTricks.Dequeue();
            if (readiness == TrickReadiness.Ready)
            {
                _trick = next;
                _trickAsked = false;
                _trickStarted = false;
                _trickLoopedFor = TimeSpan.Zero;
            }
        }
    }

    /// <returns>True while the trick still has the character's attention.</returns>
    private bool ContinueTrick(TimeSpan elapsed)
    {
        var trick = _trick!;
        if (_player.Current == trick)
        {
            _trickStarted = true;
            if (!trick.Loop)
            {
                return true;
            }

            _trickLoopedFor += elapsed;
            if (_trickLoopedFor < TrickLoopTime)
            {
                return true;
            }

            // Long enough: come out of the loop properly, through its finishing animation if it has one.
            _trick = null;
            _player.Request(_manifest.Default.Name, Priority);
            return true;
        }

        if (_trickStarted)
        {
            // A single movement that has now finished. If another is waiting and ready it starts straight away.
            _trick = null;
            TakeNextTrick();
            if (_trick is null)
            {
                return false;
            }

            trick = _trick;
        }

        if (!_trickAsked)
        {
            // Asked for once only: asking again during a lead-in would restart it.
            _trickAsked = _player.Request(trick.Name, Priority);
            return true;
        }

        if (_player.Current.Loop)
        {
            // Something else took its place before it could start, such as a trick chosen from the menu.
            // The user has chosen something else, so anything still waiting is dropped too.
            _trick = null;
            _waitingTricks.Clear();
            return false;
        }

        return true;
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

        _talkStarted = true;
    }

    // Only talking that the conversation started is ended here. Talking chosen from the rider's menu is left alone.
    private void Rest()
    {
        if (!_talkStarted)
        {
            return;
        }

        _talkStarted = false;
        if (_talking is not null && _player.Current == _talking)
        {
            _player.Request(_manifest.Default.Name, Priority);
        }
    }
}
