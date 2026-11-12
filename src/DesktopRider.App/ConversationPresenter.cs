using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using DesktopRider.AI;
using DesktopRider.Animation;
using DesktopRider.Core.Placement;
using DesktopRider.Core.ViewModels;
using DesktopRider.Desktop;

namespace DesktopRider.App;

/// <summary>
/// Joins the conversation to the desktop: opens and closes the conversation window beside the rider, shows the
/// speech bubble, and tells the rider's animation what the conversation is doing. The decisions are made by the
/// view models; this class only carries them out with real windows, which are created the first time they are needed.
/// </summary>
internal sealed class ConversationPresenter : IDisposable
{
    // The rider's window has an empty margin around the artwork, so the bubble is tucked slightly into it.
    private const int BubbleGap = -12;
    private const int ChatGap = -6;

    private readonly OverlayWindow _overlay;
    private readonly OverlayViewModel _overlayViewModel;
    private readonly ConversationViewModel _conversation;
    private readonly SpeechBubbleViewModel _bubble;
    private readonly IScreenProvider _screens;
    private readonly DispatcherTimer _bubbleTimer;
    private ChatWindow? _chatWindow;
    private CompanionWindowPlacer? _chatPlacer;
    private SpeechBubbleWindow? _bubbleWindow;
    private CompanionWindowPlacer? _bubblePlacer;
    private bool _openWhenShown;
    private bool _disposed;

    public ConversationPresenter(
        OverlayWindow overlay,
        OverlayViewModel overlayViewModel,
        ConversationViewModel conversation,
        SpeechBubbleViewModel bubble,
        IScreenProvider screens)
    {
        _overlay = overlay;
        _overlayViewModel = overlayViewModel;
        _conversation = conversation;
        _bubble = bubble;
        _screens = screens;
        _bubbleTimer = new DispatcherTimer(DispatcherPriority.Background, overlay.Dispatcher);
        _bubbleTimer.Tick += OnBubbleTimeUp;

        _overlay.TalkRequested += OnTalkRequested;
        _overlay.IsVisibleChanged += OnOverlayVisibleChanged;
        _overlay.Closed += OnExiting;
        _overlayViewModel.ExitRequested += OnExiting;
        _overlayViewModel.PropertyChanged += OnOverlayViewModelChanged;
        _conversation.PropertyChanged += OnConversationChanged;
        _bubble.PropertyChanged += OnBubbleChanged;
    }

    /// <summary>Opens the conversation window, bringing the rider back first if it is hidden.</summary>
    public void OpenConversation()
    {
        if (_disposed)
        {
            return;
        }

        if (!_overlay.IsVisible)
        {
            _openWhenShown = true;
            _overlayViewModel.ShowCommand.Execute(null);
            return;
        }

        if (_chatWindow is null)
        {
            _chatWindow = new ChatWindow(_conversation) { Topmost = _overlayViewModel.AlwaysOnTop };
            _chatWindow.IsVisibleChanged += OnChatVisibleChanged;
            _chatPlacer = new CompanionWindowPlacer(_chatWindow, _overlay, _screens, ChatGap);
            _chatPlacer.Attach();
        }

        _chatWindow.Show();
        _chatPlacer?.Place();
        _chatWindow.FocusInput();

        // Opening the conversation is the user asking about the AI, so this is when its state is looked up.
        _conversation.CheckConnectionCommand.Execute(null);
    }

    /// <summary>What a click on the rider does: opens the conversation window, or closes it if it is open.</summary>
    public void ToggleConversation()
    {
        if (_chatWindow is { IsVisible: true })
        {
            _chatWindow.Hide();
        }
        else
        {
            OpenConversation();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _bubbleTimer.Stop();
        _bubbleTimer.Tick -= OnBubbleTimeUp;
        _overlay.TalkRequested -= OnTalkRequested;
        _overlay.IsVisibleChanged -= OnOverlayVisibleChanged;
        _overlay.Closed -= OnExiting;
        _overlayViewModel.ExitRequested -= OnExiting;
        _overlayViewModel.PropertyChanged -= OnOverlayViewModelChanged;
        _conversation.PropertyChanged -= OnConversationChanged;
        _bubble.PropertyChanged -= OnBubbleChanged;
        _chatPlacer?.Dispose();
        _bubblePlacer?.Dispose();
        if (_chatWindow is not null)
        {
            _chatWindow.IsVisibleChanged -= OnChatVisibleChanged;
            _chatWindow.ClosingForGood = true;
            _chatWindow.Close();
        }

        if (_bubbleWindow is not null)
        {
            _bubbleWindow.Clicked -= OnBubbleClicked;
            _bubbleWindow.Close();
        }
    }

    // Desktop Rider is closing: the conversation window and speech bubble go with the rider.
    private void OnExiting(object? sender, EventArgs e) => Dispose();

    private void OnTalkRequested(object? sender, EventArgs e) => ToggleConversation();

    private void OnBubbleClicked(object? sender, EventArgs e) => OpenConversation();

    private void OnChatVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        _bubble.ConversationOpen = _chatWindow is { IsVisible: true };

    private void OnOverlayVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_overlay.IsVisible)
        {
            if (_openWhenShown)
            {
                _openWhenShown = false;

                // Wait until the rider has been put in its place, so the conversation opens beside it.
                _overlay.Dispatcher.BeginInvoke(OpenConversation, DispatcherPriority.Loaded);
            }
        }
        else
        {
            // A hidden rider takes its conversation window and speech bubble with it.
            _chatWindow?.Hide();
            _bubble.Dismiss();
        }
    }

    // Raised from whichever thread saved the settings, so the windows are only touched from their own thread.
    private void OnOverlayViewModelChanged(object? sender, PropertyChangedEventArgs e) => OnInterfaceThread(() =>
    {
        var topmost = _overlayViewModel.AlwaysOnTop;
        if (_chatWindow is not null)
        {
            _chatWindow.Topmost = topmost;
        }

        if (_bubbleWindow is not null)
        {
            _bubbleWindow.Topmost = topmost;
        }
    });

    private void OnConversationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConversationViewModel.State))
        {
            OnInterfaceThread(() => _overlay.CharacterState = _conversation.State switch
            {
                ConversationState.Thinking => CharacterState.Thinking,
                ConversationState.Talking => CharacterState.Talking,
                _ => CharacterState.Idle,
            });
        }
    }

    private void OnBubbleChanged(object? sender, PropertyChangedEventArgs e) => OnInterfaceThread(() =>
    {
        if (e.PropertyName == nameof(SpeechBubbleViewModel.HideAfter))
        {
            _bubbleTimer.Stop();
            if (_bubble.HideAfter is { } wait)
            {
                _bubbleTimer.Interval = wait;
                _bubbleTimer.Start();
            }
        }
        else if (e.PropertyName == nameof(SpeechBubbleViewModel.IsVisible))
        {
            ShowOrHideBubble();
        }
    });

    private void OnBubbleTimeUp(object? sender, EventArgs e)
    {
        _bubbleTimer.Stop();
        _bubble.Dismiss();
    }

    private void ShowOrHideBubble()
    {
        if (_disposed)
        {
            return;
        }

        if (!_bubble.IsVisible || !_overlay.IsVisible)
        {
            _bubbleWindow?.Hide();
            return;
        }

        if (_bubbleWindow is null)
        {
            _bubbleWindow = new SpeechBubbleWindow(_bubble) { Topmost = _overlayViewModel.AlwaysOnTop };
            _bubbleWindow.Clicked += OnBubbleClicked;
            _bubblePlacer = new CompanionWindowPlacer(_bubbleWindow, _overlay, _screens, BubbleGap);
            _bubblePlacer.SideChanged += (_, _) => _bubbleWindow.TailVisible = _bubblePlacer.Side == CompanionSide.Above;
            _bubblePlacer.Attach();
        }

        _bubbleWindow.Show();
        _bubblePlacer?.Place();
    }

    private void OnInterfaceThread(Action action)
    {
        var dispatcher = _overlay.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.BeginInvoke(action);
        }
    }
}
