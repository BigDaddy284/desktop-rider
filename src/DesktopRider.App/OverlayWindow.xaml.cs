using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DesktopRider.Animation;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Placement;
using DesktopRider.Core.ViewModels;
using DesktopRider.Desktop;
using Microsoft.Extensions.Logging;

namespace DesktopRider.App;

/// <summary>The transparent, borderless window the character lives in.</summary>
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The placement controller is disposed when the window closes.")]
public partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _viewModel;
    private readonly WindowPlacementController _placement;
    private readonly ISettingsService _settings;
    private readonly RiderSprite? _rider;
    private StageWindow? _stage;
    private bool _onStage;
    private bool _leavingStage;
    private double _size = RiderSprite.DisplayFactor;
    private bool _closed;

    public OverlayWindow(
        OverlayViewModel viewModel,
        ISettingsService settings,
        IScreenProvider screens,
        ILogger<WindowPlacementController> placementLogger,
        ILogger<OverlayWindow> logger)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _settings = settings;

        _rider = RiderSprite.TryLoad(Path.Combine(AppContext.BaseDirectory, "Assets", "Sprites", "Rider"), logger);
        if (_rider is not null)
        {
            StandIn.Visibility = Visibility.Collapsed;
            RiderImage.Visibility = Visibility.Visible;
            TurnAroundItem.Visibility = Visibility.Visible;
            BuildTricksMenu(_rider);
            _rider.FrameChanged += OnRiderFrameChanged;
            LocationChanged += OnLocationChanged;
            _settings.Changed += OnSettingsChanged;
            IsVisibleChanged += OnIsVisibleChanged;
            ApplySettings();
            RiderImage.Source = _rider.CurrentFrame;
        }

        _placement = new WindowPlacementController(this, settings, screens, placementLogger);
        _placement.Attach();
        _placement.Clicked += OnCharacterClicked;
        _viewModel.ResetPositionRequested += OnResetPositionRequested;
        _viewModel.ExitRequested += OnExitRequested;
        _viewModel.PropertyChanged += OnViewModelChanged;
    }

    /// <summary>Raised when the user clicks the rider or chooses "Talk to the rider" from its menu.</summary>
    internal event EventHandler? TalkRequested;

    /// <summary>Raised when the user chooses "Rider settings" from the rider's menu.</summary>
    internal event EventHandler? SettingsRequested;

    /// <summary>Raised when the user chooses to hush the rider, or to let it speak again, from the rider's menu.</summary>
    internal event EventHandler? QuietToggleRequested;

    /// <summary>
    /// Supplies the words for the "keep quiet" line of the rider's menu each time the menu opens.
    /// Null words leave the line out, which is right while the rider never speaks first anyway.
    /// </summary>
    internal Func<string?>? QuietChoice { get; set; }

    /// <summary>What the rider is doing in the conversation, so that its animation can follow.</summary>
    internal CharacterState CharacterState
    {
        set
        {
            if (_rider is not null)
            {
                _rider.State = value;
            }
        }
    }

    /// <summary>Makes the rider perform an animation once, such as a wheelie asked for in conversation.</summary>
    internal void Perform(string animation) => _rider?.Perform(animation);

    /// <summary>The rider's current picture, used to make the notification-area icon. Null while the stand-in is showing.</summary>
    internal BitmapSource? RiderPicture => _rider?.CurrentFrame as BitmapSource;

    /// <summary>Shows or hides the window to match the user's choice. Hiding also stops the animation timer.</summary>
    internal void ApplyVisibility()
    {
        if (_closed)
        {
            return;
        }

        if (_viewModel.CharacterVisible && !IsVisible)
        {
            Show();
        }
        else if (!_viewModel.CharacterVisible && IsVisible)
        {
            Hide();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _viewModel.PropertyChanged -= OnViewModelChanged;
        _viewModel.ResetPositionRequested -= OnResetPositionRequested;
        _viewModel.ExitRequested -= OnExitRequested;
        _placement.Clicked -= OnCharacterClicked;
        _placement.Dispose();
        if (_rider is not null)
        {
            _settings.Changed -= OnSettingsChanged;
            IsVisibleChanged -= OnIsVisibleChanged;
            _rider.FrameChanged -= OnRiderFrameChanged;
            LocationChanged -= OnLocationChanged;
            _rider.Dispose();
            _stage?.Close();
        }

        base.OnClosed(e);
    }

    private async void OnResetPositionRequested(object? sender, EventArgs e) => await _placement.ResetPositionAsync();

    // Settings can change on a background thread, so the window is only touched from its own thread.
    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Dispatcher.BeginInvoke(ApplyVisibility);

    private void OnExitRequested(object? sender, EventArgs e) => Close();

    /// <summary>Turns an animation name such as "LookLeft" into the words shown on the menu.</summary>
    internal static string FriendlyName(string animation) =>
        string.Concat(animation.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));

    private void BuildTricksMenu(RiderSprite rider)
    {
        var linked = rider.Manifest.Animations.SelectMany(clip => new[] { clip.Enter, clip.Exit }).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var clip in rider.Manifest.Animations.Where(clip => !linked.Contains(clip.Name)))
        {
            var item = new MenuItem { Header = FriendlyName(clip.Name), Tag = clip.Name };
            item.Click += OnTrickClick;
            TricksMenu.Items.Add(item);
        }

        TricksMenu.Visibility = Visibility.Visible;
    }

    private void OnTrickClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string name })
        {
            _rider?.Play(name, priority: 1);
        }
    }

    private void OnCharacterClicked(object? sender, EventArgs e) => TalkRequested?.Invoke(this, EventArgs.Empty);

    private void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        var words = QuietChoice?.Invoke();
        QuietItem.Header = words ?? string.Empty;
        QuietItem.Visibility = words is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnQuietClick(object sender, RoutedEventArgs e) => QuietToggleRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsClick(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnTalkClick(object sender, RoutedEventArgs e) => TalkRequested?.Invoke(this, EventArgs.Empty);

    private void OnTurnAroundClick(object sender, RoutedEventArgs e)
    {
        if (_rider is not null)
        {
            _rider.Facing = _rider.Facing == Facing.Right ? Facing.Left : Facing.Right;
        }
    }

    private void OnRiderFrameChanged(object? sender, EventArgs e)
    {
        if (_rider?.CurrentFrame is not { } frame)
        {
            return;
        }

        if (_rider.OnStage)
        {
            // A jump or another big animation: it is drawn in a larger see-through window laid exactly over the rider.
            // The rider's own picture is hidden from the second frame on, so there is never a moment with neither showing.
            _stage ??= new StageWindow { Owner = this };
            _stage.Topmost = Topmost;
            var (width, height) = _rider.FrameSize;
            _stage.Present(frame, new Size(width * _size, height * _size), StageCorner(), CharacterRoot.Opacity);
            if (_onStage)
            {
                RiderImage.Opacity = 0;
            }

            _onStage = true;
            _leavingStage = false;
            return;
        }

        RiderImage.Source = frame;
        if (_onStage)
        {
            // Back in the rider's own window. The big window is put away one frame later, for the same reason.
            RiderImage.Opacity = 1;
            _onStage = false;
            _leavingStage = true;
        }
        else if (_leavingStage)
        {
            _leavingStage = false;
            _stage?.Hide();
        }
    }

    /// <summary>Where the big window's top-left corner must be for its rider to sit exactly on top of the usual one.</summary>
    private Point StageCorner()
    {
        var rider = RiderImage.TranslatePoint(default, this);
        var (x, y) = _rider!.FrameOrigin;
        return new Point(Left + rider.X - (x * _size), Top + rider.Y - (y * _size));
    }

    private void OnLocationChanged(object? sender, EventArgs e)
    {
        if (_stage is { IsVisible: true } && _rider is { OnStage: true })
        {
            _stage.MoveTo(StageCorner());
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => Dispatcher.Invoke(ApplySettings);

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            _rider?.Start();
        }
        else
        {
            _rider?.Stop();
            _stage?.Hide();
        }
    }

    private void ApplySettings()
    {
        if (_rider is null)
        {
            return;
        }

        var appearance = _settings.Current.Appearance;
        var size = RiderSprite.DisplayFactor * Math.Clamp(appearance.Scale, AppearanceSettings.MinScale, AppearanceSettings.MaxScale);
        _size = size;
        RiderImage.Width = _rider.Manifest.FrameWidth * size;
        RiderImage.Height = _rider.Manifest.FrameHeight * size;
        _rider.Speed = Math.Clamp(appearance.AnimationSpeed, AppearanceSettings.MinAnimationSpeed, AppearanceSettings.MaxAnimationSpeed);
        _rider.IdleAnimations = _settings.Current.Behaviour.IdleAnimations;
    }
}
