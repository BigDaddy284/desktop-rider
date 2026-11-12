using System.ComponentModel;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.AI;

/// <summary>
/// What the speech bubble beside the rider says. The bubble is for when the conversation window is closed:
/// it shows that the rider is thinking, the reply as it arrives, and anything that went wrong.
/// While the conversation window is open the same words are already there, so the bubble stays away.
/// </summary>
public sealed class SpeechBubbleViewModel : ViewModelBase, IDisposable
{
    public const string ThinkingText = "Thinking…";

    /// <summary>The most characters shown. A longer reply is shortened; the whole of it is in the conversation window.</summary>
    public const int MaxLength = 280;

    public static readonly TimeSpan ShortestStay = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan LongestStay = TimeSpan.FromSeconds(25);

    private readonly ConversationViewModel _conversation;
    private string _text = string.Empty;
    private bool _visible;
    private bool _conversationOpen;
    private TimeSpan? _hideAfter;

    public SpeechBubbleViewModel(ConversationViewModel conversation)
    {
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _conversation.PropertyChanged += OnConversationChanged;
        _conversation.NoticeAdded += OnNoticeAdded;
    }

    public string Text
    {
        get => _text;
        private set
        {
            if (_text != value)
            {
                _text = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsVisible
    {
        get => _visible;
        private set
        {
            if (_visible != value)
            {
                _visible = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// How long the bubble should stay before it is dismissed, once what it says is complete.
    /// Null while the rider is still thinking or talking, when it must stay.
    /// </summary>
    public TimeSpan? HideAfter
    {
        get => _hideAfter;
        private set
        {
            _hideAfter = value;

            // Always announced, so that a new message of the same length restarts the wait.
            OnPropertyChanged();
        }
    }

    /// <summary>Set while the conversation window is showing. The bubble disappears and stays away.</summary>
    public bool ConversationOpen
    {
        get => _conversationOpen;
        set
        {
            if (_conversationOpen == value)
            {
                return;
            }

            _conversationOpen = value;
            if (value)
            {
                Dismiss();
            }
            else
            {
                // Closing the window part-way through a reply hands the rest of it to the bubble.
                Refresh();
            }
        }
    }

    /// <summary>Takes the bubble away: the user clicked it, its time ran out, or the rider was hidden.</summary>
    public void Dismiss()
    {
        IsVisible = false;
        HideAfter = null;
    }

    /// <summary>How long to leave words on screen so they can be read comfortably.</summary>
    public static TimeSpan ReadingTime(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var seconds = 3 + (text.Length * 0.06);
        return TimeSpan.FromSeconds(Math.Clamp(seconds, ShortestStay.TotalSeconds, LongestStay.TotalSeconds));
    }

    /// <summary>Fits a reply that is still arriving into the bubble by showing its newest words.</summary>
    public static string NewestPart(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= MaxLength)
        {
            return text;
        }

        var tail = text[^(MaxLength - 1)..];
        var space = tail.IndexOf(' ', StringComparison.Ordinal);
        if (space >= 0 && space < 24)
        {
            tail = tail[(space + 1)..];
        }

        return "…" + tail;
    }

    /// <summary>Fits a finished reply into the bubble by showing how it starts.</summary>
    public static string FirstPart(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= MaxLength)
        {
            return text;
        }

        var head = text[..(MaxLength - 1)];
        var space = head.LastIndexOf(' ');
        if (space > MaxLength - 24)
        {
            head = head[..space];
        }

        return head.TrimEnd() + "…";
    }

    public void Dispose()
    {
        _conversation.PropertyChanged -= OnConversationChanged;
        _conversation.NoticeAdded -= OnNoticeAdded;
    }

    private void OnConversationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConversationViewModel.State) or nameof(ConversationViewModel.CurrentReply))
        {
            Refresh(finished: e.PropertyName == nameof(ConversationViewModel.State));
        }
    }

    private void OnNoticeAdded(object? sender, ConversationNoticeEventArgs e)
    {
        if (!_conversationOpen)
        {
            Say(FirstPart(e.Message), stay: true);
        }
    }

    private void Refresh(bool finished = false)
    {
        if (_conversationOpen)
        {
            return;
        }

        switch (_conversation.State)
        {
            case ConversationState.Thinking:
                Say(ThinkingText, stay: false);
                break;
            case ConversationState.Talking:
                Say(_conversation.CurrentReply.Length == 0 ? ThinkingText : NewestPart(_conversation.CurrentReply), stay: false);
                break;
            default:
                // A cleared conversation leaves nothing to say. A problem is shown by its own notice.
                if (_conversation.Lines.Count == 0)
                {
                    Dismiss();
                }
                else if (finished && _conversation.CurrentReply.Length > 0 && _conversation.Lines[^1].Speaker == Speaker.Rider)
                {
                    Say(FirstPart(_conversation.CurrentReply), stay: true);
                }

                break;
        }
    }

    private void Say(string words, bool stay)
    {
        Text = words;
        IsVisible = true;
        HideAfter = stay ? ReadingTime(words) : null;
    }
}
