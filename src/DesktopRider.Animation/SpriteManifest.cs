using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopRider.Animation;

/// <summary>Which way the bike points on screen.</summary>
public enum Facing
{
    Right,
    Left,
}

/// <summary>Where one frame sits inside its sprite sheet, in pixels.</summary>
public readonly record struct FrameRect(int X, int Y, int Width, int Height);

/// <summary>One animation: a strip of equally sized frames laid out in a grid on a sprite sheet.</summary>
public sealed class AnimationClip
{
    public required string Name { get; init; }

    /// <summary>True when the animation repeats until something else is asked for.</summary>
    public bool Loop { get; init; }

    public required int FrameCount { get; init; }

    /// <summary>How many frames sit side by side on each row of the sheet.</summary>
    public required int Columns { get; init; }

    /// <summary>Sheet file for the bike facing right, relative to the manifest.</summary>
    public required string Right { get; init; }

    /// <summary>Sheet file for the bike facing left, relative to the manifest.</summary>
    public required string Left { get; init; }

    /// <summary>A one-shot animation that must play before this one starts, such as nodding off before sleeping.</summary>
    public string? Enter { get; init; }

    /// <summary>A one-shot animation that must play when this one is left, such as waking up.</summary>
    public string? Exit { get; init; }

    /// <summary>
    /// Width of this animation's frames in pixels, when it needs more room than the standard frame, such as a jump.
    /// Left out for ordinary animations.
    /// </summary>
    public int? FrameWidth { get; init; }

    /// <summary>Height of this animation's frames in pixels, when it needs more room than the standard frame.</summary>
    public int? FrameHeight { get; init; }

    /// <summary>How far the standard frame sits from the left edge of this animation's larger frame, with the bike facing right.</summary>
    public int OriginX { get; init; }

    /// <summary>How far the standard frame sits from the top edge of this animation's larger frame.</summary>
    public int OriginY { get; init; }

    /// <summary>True when the animation has frames larger than the standard one, so it needs room around the character.</summary>
    [JsonIgnore]
    public bool HasOwnFrame => FrameWidth is not null || FrameHeight is not null;

    public string SheetFor(Facing facing) => facing == Facing.Right ? Right : Left;
}

/// <summary>Describes a character's sprite sheets. Adding an animation only needs a new entry here and its two sheets.</summary>
public sealed class SpriteManifest
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };

    private Dictionary<string, AnimationClip> _byName = new(StringComparer.OrdinalIgnoreCase);

    public required int FrameWidth { get; init; }

    public required int FrameHeight { get; init; }

    public required double FramesPerSecond { get; init; }

    /// <summary>Distance from the top of a frame to the ground line the wheels stand on, in pixels.</summary>
    public int GroundY { get; init; }

    /// <summary>The looping animation the character rests in.</summary>
    public string DefaultAnimation { get; init; } = "Idle";

    public required IReadOnlyList<AnimationClip> Animations { get; init; }

    [JsonIgnore]
    public AnimationClip Default => this[DefaultAnimation];

    public AnimationClip this[string name] =>
        _byName.TryGetValue(name, out var clip) ? clip : throw new KeyNotFoundException($"There is no animation called '{name}'.");

    public bool TryGet(string name, out AnimationClip clip) => _byName.TryGetValue(name, out clip!);

    /// <summary>The size of one frame of an animation in pixels.</summary>
    public (int Width, int Height) FrameSizeFor(AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return (clip.FrameWidth ?? FrameWidth, clip.FrameHeight ?? FrameHeight);
    }

    /// <summary>
    /// Where the standard frame sits inside an animation's own frame, in pixels from its top-left corner.
    /// Drawing the animation's frame this far up and to the left of the standard one keeps the character exactly in place.
    /// The left-facing sheets are mirror images, so the distance is measured from the other edge.
    /// </summary>
    public (int X, int Y) OriginFor(AnimationClip clip, Facing facing)
    {
        ArgumentNullException.ThrowIfNull(clip);
        var x = facing == Facing.Right ? clip.OriginX : FrameSizeFor(clip).Width - FrameWidth - clip.OriginX;
        return (x, clip.OriginY);
    }

    /// <summary>Works out where a frame sits on its sheet.</summary>
    public FrameRect FrameRectFor(AnimationClip clip, int frameIndex)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(frameIndex, clip.FrameCount);
        var (width, height) = FrameSizeFor(clip);
        return new FrameRect(frameIndex % clip.Columns * width, frameIndex / clip.Columns * height, width, height);
    }

    /// <summary>Reads and checks a manifest. Throws <see cref="InvalidDataException"/> with a plain explanation if it is not usable.</summary>
    public static SpriteManifest Parse(string json)
    {
        SpriteManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<SpriteManifest>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The sprite list could not be read: {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new InvalidDataException("The sprite list is empty.");
        }

        manifest.Validate();
        return manifest;
    }

    private void Validate()
    {
        if (FrameWidth <= 0 || FrameHeight <= 0)
        {
            throw new InvalidDataException("The sprite list must give a frame width and height above zero.");
        }

        if (!(FramesPerSecond > 0) || double.IsInfinity(FramesPerSecond))
        {
            throw new InvalidDataException("The sprite list must give a frames-per-second value above zero.");
        }

        if (Animations is null || Animations.Count == 0)
        {
            throw new InvalidDataException("The sprite list does not contain any animations.");
        }

        var byName = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        foreach (var clip in Animations)
        {
            if (clip is null || string.IsNullOrWhiteSpace(clip.Name))
            {
                throw new InvalidDataException("Every animation in the sprite list needs a name.");
            }

            if (clip.FrameCount <= 0 || clip.Columns <= 0)
            {
                throw new InvalidDataException($"Animation '{clip.Name}' needs a frame count and column count above zero.");
            }

            if (string.IsNullOrWhiteSpace(clip.Right) || string.IsNullOrWhiteSpace(clip.Left))
            {
                throw new InvalidDataException($"Animation '{clip.Name}' needs a sheet for each direction.");
            }

            var (width, height) = FrameSizeFor(clip);
            if (clip.OriginX < 0 || clip.OriginY < 0 || clip.OriginX + FrameWidth > width || clip.OriginY + FrameHeight > height)
            {
                throw new InvalidDataException($"Animation '{clip.Name}' has a frame that does not contain the standard frame at the place it gives.");
            }

            if (!byName.TryAdd(clip.Name, clip))
            {
                throw new InvalidDataException($"Animation '{clip.Name}' is listed more than once.");
            }
        }

        foreach (var clip in Animations)
        {
            foreach (var linked in new[] { clip.Enter, clip.Exit })
            {
                if (linked is null)
                {
                    continue;
                }

                if (!byName.TryGetValue(linked, out var target))
                {
                    throw new InvalidDataException($"Animation '{clip.Name}' refers to '{linked}', which is not in the sprite list.");
                }

                if (target.Loop || !clip.Loop)
                {
                    throw new InvalidDataException($"Animation '{clip.Name}' can only use a one-shot animation to start or finish a loop.");
                }
            }
        }

        if (!byName.TryGetValue(DefaultAnimation, out var fallback) || !fallback.Loop || fallback.Enter is not null || fallback.Exit is not null)
        {
            throw new InvalidDataException($"The resting animation '{DefaultAnimation}' must be a plain looping animation in the sprite list.");
        }

        _byName = byName;
    }
}
