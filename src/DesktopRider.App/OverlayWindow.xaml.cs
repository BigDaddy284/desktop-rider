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
            _rider.Dispose();
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

    private void OnTalkClick(object sender, RoutedEventArgs e) => TalkRequested?.Invoke(this, EventArgs.Empty);

    private void OnTurnAroundClick(object sender, RoutedEventArgs e)
    {
        if (_rider is not null)
        {
            _rider.Facing = _rider.Facing == Facing.Right ? Facing.Left : Facing.Right;
        }
    }

    private void OnRiderFrameChanged(object? sender, EventArgs e) => RiderImage.Source = _rider?.CurrentFrame;

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
        RiderImage.Width = _rider.Manifest.FrameWidth * size;
        RiderImage.Height = _rider.Manifest.FrameHeight * size;
        _rider.Speed = Math.Clamp(appearance.AnimationSpeed, AppearanceSettings.MinAnimationSpeed, AppearanceSettings.MaxAnimationSpeed);
        _rider.IdleAnimations = _settings.Current.Behaviour.IdleAnimations;
    }
}
