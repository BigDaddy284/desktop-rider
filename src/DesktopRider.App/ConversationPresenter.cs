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
/// Joins the conversation to the desktop. A click on the rider opens whichever way of talking the user has chosen:
/// the conversation window, or (with "Chat in bubbles") a small typing bubble below the rider. It also shows the
/// rider's speech bubble and tells the rider's animation what the conversation is doing. The decisions are made by
/// the view models; this class only carries them out with real windows, created the first time they are needed.
/// </summary>
internal sealed class ConversationPresenter : IDisposable
{
    // The rider's window has an empty margin around the artwork, so its companions are tucked slightly into it.
    private const int BubbleGap = -12;
    private const int ChatGap = -6;
    private const int InputGap = -12;

    private readonly OverlayWindow _overlay;
    private readonly OverlayViewModel _overlayViewModel;
    private readonly ConversationViewModel _conversation;
    private readonly SpeechBubbleViewModel _bubble;
    private readonly IScreenProvider _screens;
    private readonly DispatcherTimer _bubbleTimer;
    private ChatWindow? _chatWindow;
    private CompanionWindowPlacer? _chatPlacer;
    private InputBubbleWindow? _inputWindow;
    private CompanionWindowPlacer? _inputPlacer;
    private SpeechBubbleWindow? _bubbleWindow;
    private CompanionWindowPlacer? _bubblePlacer;
    private bool _inBubbles;
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
        _inBubbles = overlayViewModel.ChatInBubbles;
        _bubbleTimer = new DispatcherTimer(DispatcherPriority.Background, overlay.Dispatcher);
        _bubbleTimer.Tick += OnBubbleTimeUp;

        _overlay.TalkRequested += OnTalkRequested;
        _overlay.IsVisibleChanged += OnOverlayVisibleChanged;
        _overlay.Closed += OnExiting;
        _overlayViewModel.ExitRequested += OnExiting;
        _overlayViewModel.PropertyChanged += OnOverlayViewModelChanged;
        _conversation.PropertyChanged += OnConversationChanged;
        _conversation.ActionRequested += OnActionRequested;
        _bubble.PropertyChanged += OnBubbleChanged;
    }

    private bool ChatShowing => _chatWindow is { IsVisible: true };

    private bool InputShowing => _inputWindow is { IsVisible: true };

    /// <summary>Opens the conversation in the way the user has chosen, bringing the rider back first if it is hidden.</summary>
    public void OpenConversation()
    {
        if (_inBubbles)
        {
            OpenTypingBubble();
        }
        else
        {
            OpenConversationWindow();
        }
    }

    /// <summary>What a click on the rider does: opens the conversation, or puts it away if it is open.</summary>
    public void ToggleConversation()
    {
        if (ChatShowing || InputShowing)
        {
            _chatWindow?.Hide();
            _inputWindow?.Hide();
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
        _conversation.ActionRequested -= OnActionRequested;
        _bubble.PropertyChanged -= OnBubbleChanged;
        _chatPlacer?.Dispose();
        _inputPlacer?.Dispose();
        _bubblePlacer?.Dispose();
        if (_chatWindow is not null)
        {
            _chatWindow.IsVisibleChanged -= OnChatVisibleChanged;
            _chatWindow.BubblesRequested -= OnBubblesRequested;
            _chatWindow.ClosingForGood = true;
            _chatWindow.Close();
        }

        if (_inputWindow is not null)
        {
            _inputWindow.IsVisibleChanged -= OnInputVisibleChanged;
            _inputWindow.WindowRequested -= OnWindowRequested;
            _inputWindow.ClosingForGood = true;
            _inputWindow.Close();
        }

        if (_bubbleWindow is not null)
        {
            _bubbleWindow.Clicked -= OnBubbleClicked;
            _bubbleWindow.Close();
        }
    }

    /// <summary>Opens the conversation window, with everything said so far.</summary>
    private void OpenConversationWindow()
    {
        if (!ReadyToOpen())
        {
            return;
        }

        if (_chatWindow is null)
        {
            _chatWindow = new ChatWindow(_conversation) { Topmost = _overlayViewModel.AlwaysOnTop };
            _chatWindow.IsVisibleChanged += OnChatVisibleChanged;
            _chatWindow.BubblesRequested += OnBubblesRequested;
            _chatPlacer = new CompanionWindowPlacer(_chatWindow, _overlay, _screens, ChatGap);
            _chatPlacer.Attach();
        }

        _inputWindow?.Hide();
        _chatWindow.Show();
        _chatPlacer?.Place();
        _chatWindow.FocusInput();

        // Opening the conversation is the user asking about the AI, so this is when its state is looked up.
        _conversation.CheckConnectionCommand.Execute(null);
    }

    /// <summary>Opens the small typing bubble below the rider. The rider answers in its speech bubble.</summary>
    private void OpenTypingBubble()
    {
        if (!ReadyToOpen())
        {
            return;
        }

        if (_inputWindow is null)
        {
            _inputWindow = new InputBubbleWindow(_conversation) { Topmost = _overlayViewModel.AlwaysOnTop };
            _inputWindow.IsVisibleChanged += OnInputVisibleChanged;
            _inputWindow.WindowRequested += OnWindowRequested;
            _inputPlacer = new CompanionWindowPlacer(_inputWindow, _overlay, _screens, InputGap, CompanionSide.Below);
            _inputPlacer.SideChanged += (_, _) => _inputWindow.TailVisible = _inputPlacer.Side == CompanionSide.Below;
            _inputPlacer.Attach();
        }

        _chatWindow?.Hide();
        _inputWindow.Show();
        _inputPlacer?.Place();
        _inputWindow.FocusInput();
    }

    // A hidden rider is brought back first; the conversation then opens once it is in its place.
    private bool ReadyToOpen()
    {
        if (_disposed)
        {
            return false;
        }

        if (!_overlay.IsVisible)
        {
            _openWhenShown = true;
            _overlayViewModel.ShowCommand.Execute(null);
            return false;
        }

        return true;
    }

    private void OnTalkRequested(object? sender, EventArgs e) => ToggleConversation();

    // The bubble only ever shows part of a long reply, so clicking it opens the window with all of it.
    private void OnBubbleClicked(object? sender, EventArgs e) => OpenConversationWindow();

    private void OnBubblesRequested(object? sender, EventArgs e) => _ = _overlayViewModel.SetChatInBubblesAsync(true);

    private void OnWindowRequested(object? sender, EventArgs e) => _ = _overlayViewModel.SetChatInBubblesAsync(false);

    private void OnChatVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => _bubble.ConversationOpen = ChatShowing;

    // While the typing bubble is open the rider's last reply stays up; putting the bubble away takes the reply with it.
    private void OnInputVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => _bubble.KeepOpen = InputShowing;

    private void OnExiting(object? sender, EventArgs e) => Dispose();

    private void OnOverlayVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_overlay.IsVisible)
        {
            if (_openWhenShown)
            {
                _openWhenShown = false;
                _overlay.Dispatcher.BeginInvoke(OpenConversation, DispatcherPriority.Loaded);
            }
        }
        else
        {
            // A hidden rider takes its conversation and speech bubble with it.
            _chatWindow?.Hide();
            _inputWindow?.Hide();
            _bubble.Dismiss();
        }
    }

    // Raised from whichever thread saved the settings, so the windows are only touched from their own thread.
    private void OnOverlayViewModelChanged(object? sender, PropertyChangedEventArgs e) => OnInterfaceThread(() =>
    {
        if (_disposed)
        {
            return;
        }

        var topmost = _overlayViewModel.AlwaysOnTop;
        foreach (var window in new Window?[] { _chatWindow, _inputWindow, _bubbleWindow })
        {
            if (window is not null)
            {
                window.Topmost = topmost;
            }
        }

        // The user flipped "Chat in bubbles": if a conversation is open, carry it over to the other way of talking.
        var inBubbles = _overlayViewModel.ChatInBubbles;
        if (inBubbles != _inBubbles)
        {
            _inBubbles = inBubbles;
            if (ChatShowing || InputShowing)
            {
                OpenConversation();
            }
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

    // "Do a wheelie": the conversation asks, the rider performs.
    private void OnActionRequested(object? sender, RiderActionEventArgs e) => OnInterfaceThread(() => _overlay.Perform(e.Action.Animation));

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
