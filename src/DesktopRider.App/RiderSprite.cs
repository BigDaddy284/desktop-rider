using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopRider.Animation;
using Microsoft.Extensions.Logging;

namespace DesktopRider.App;

/// <summary>
/// Plays the rider's sprite animations. It owns the only animation timer, which runs at the sprites' own frame rate
/// and is stopped whenever the character is hidden, so nothing is redrawn unless a new frame is due.
/// </summary>
internal sealed partial class RiderSprite : IDisposable
{
    /// <summary>On-screen size of a sprite pixel at normal character size.</summary>
    public const double DisplayFactor = 0.45;

    private static readonly string[] IdleChoices = ["IdleVariation", "LookLeft", "LookRight"];

    private readonly string _folder;
    private readonly ILogger _logger;
    private readonly AnimationPlayer _player;
    private readonly IdleVariationPicker _idle;
    private readonly CharacterBehaviour _behaviour;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();
    private readonly Dictionary<string, BitmapSource> _sheets = new(StringComparer.OrdinalIgnoreCase);
    private Facing _facing = Facing.Right;
    private bool _failed;

    private RiderSprite(string folder, SpriteManifest manifest, ILogger logger)
    {
        _folder = folder;
        _logger = logger;
        Manifest = manifest;
        _player = new AnimationPlayer(manifest);
        _behaviour = new CharacterBehaviour(_player, manifest);
        _idle = new IdleVariationPicker(_player, IdleChoices.Where(name => manifest.TryGet(name, out _)));
        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1.0 / manifest.FramesPerSecond) };
        _timer.Tick += OnTick;
    }

    /// <summary>Raised when there is a new picture to show.</summary>
    public event EventHandler? FrameChanged;

    public SpriteManifest Manifest { get; }

    public ImageSource? CurrentFrame { get; private set; }

    public double Speed
    {
        get => _player.Speed;
        set => _player.Speed = value;
    }

    public bool IdleAnimations
    {
        get => _idle.Enabled;
        set => _idle.Enabled = value;
    }

    /// <summary>What the rider is doing in the conversation. The animation follows on the next tick.</summary>
    public CharacterState State
    {
        get => _behaviour.State;
        set => _behaviour.State = value;
    }

    /// <summary>Performs an animation once because it was asked for in conversation. False if the rider has no such animation.</summary>
    public bool Perform(string animation) => _behaviour.Perform(animation);

    public Facing Facing
    {
        get => _facing;
        set
        {
            if (_facing != value)
            {
                _facing = value;
                Refresh();
            }
        }
    }

    /// <summary>Loads the rider's sprites from a folder, or returns null (after logging why) if they are missing or unusable.</summary>
    public static RiderSprite? TryLoad(string folder, ILogger logger)
    {
        var manifestPath = Path.Combine(folder, "sprites.json");
        try
        {
            if (!File.Exists(manifestPath))
            {
                LogMissing(logger, folder);
                return null;
            }

            var sprite = new RiderSprite(folder, SpriteManifest.Parse(File.ReadAllText(manifestPath)), logger);
            sprite.Refresh();
            return sprite._failed ? null : sprite;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            LogUnusable(logger, ex, folder);
            return null;
        }
    }

    public bool Play(string animation, int priority = 0) => _player.Request(animation, priority);

    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            _clock.Restart();
            _timer.Start();
        }
    }

    public void Stop()
    {
        _timer.Stop();
        _clock.Stop();
    }

    public void Dispose()
    {
        Stop();
        _timer.Tick -= OnTick;
        _sheets.Clear();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // A sleeping laptop or a long stall should not make the rider fast-forward through everything it missed.
        var elapsed = _clock.Elapsed;
        _clock.Restart();
        if (elapsed > TimeSpan.FromSeconds(1))
        {
            elapsed = TimeSpan.FromSeconds(1);
        }

        _idle.Advance(elapsed);
        if (_behaviour.Advance(elapsed))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        var clip = _player.Current;
        var sheet = SheetFor(clip);
        if (sheet is null)
        {
            return;
        }

        var rect = Manifest.FrameRectFor(clip, _player.FrameIndex);
        if (rect.X + rect.Width > sheet.PixelWidth || rect.Y + rect.Height > sheet.PixelHeight)
        {
            Fail(clip, new InvalidDataException("The picture is smaller than the sprite list says."));
            return;
        }

        var frame = new CroppedBitmap(sheet, new Int32Rect(rect.X, rect.Y, rect.Width, rect.Height));
        frame.Freeze();
        CurrentFrame = frame;
        FrameChanged?.Invoke(this, EventArgs.Empty);
    }

    private BitmapSource? SheetFor(AnimationClip clip)
    {
        var relative = clip.SheetFor(_facing);
        if (_sheets.TryGetValue(relative, out var cached))
        {
            return cached;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(Path.GetFullPath(Path.Combine(_folder, relative)));
            image.EndInit();
            var converted = new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0);
            converted.Freeze();

            // Only the sheet being shown and the resting sheet are kept, so memory stays small however many animations exist.
            var resting = Manifest.Default.SheetFor(_facing);
            foreach (var key in _sheets.Keys.Where(key => !string.Equals(key, resting, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _sheets.Remove(key);
            }

            _sheets[relative] = converted;
            return converted;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            Fail(clip, ex);
            return null;
        }
    }

    private void Fail(AnimationClip clip, Exception ex)
    {
        _failed = true;
        LogSheetFailed(_logger, ex, clip.Name);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The rider's pictures were not found in {Folder}. Showing the stand-in instead.")]
    private static partial void LogMissing(ILogger logger, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The rider's pictures in {Folder} could not be used. Showing the stand-in instead.")]
    private static partial void LogUnusable(ILogger logger, Exception exception, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The picture for the '{Animation}' animation could not be loaded.")]
    private static partial void LogSheetFailed(ILogger logger, Exception exception, string animation);
}
