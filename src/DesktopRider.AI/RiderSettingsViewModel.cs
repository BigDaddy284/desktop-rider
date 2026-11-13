using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.AI;

/// <summary>One thing the rider remembers, as shown in the settings with its own "Forget" button.</summary>
public sealed class MemoryNoteItem(MemoryNote note, ICommand forgetCommand)
{
    public MemoryNote Note { get; } = note;

    public string Text => Note.Text;

    public ICommand ForgetCommand { get; } = forgetCommand;
}

/// <summary>
/// The rider's settings: its personality, what it keeps between sessions and whether it speaks first. Every choice takes effect and is saved the moment it is made, so there
/// is no Save button to forget. It has no window, so it is unit tested.
/// </summary>
public sealed partial class RiderSettingsViewModel : ViewModelBase, IDisposable
{
    public const string SaveFailedText = "Desktop Rider could not save that change. It applies until Desktop Rider is closed.";

    public const string ConversationDeletedText = "The conversation has been deleted.";
    public const string EverythingForgottenText = "The rider has forgotten everything.";
    public const string AlreadyRememberedText = "The rider already remembers that.";
    public const string MemoryFullText = "The rider's memory is full. Delete something to make room.";
    public const string MemoryOffText = "Switch on the rider's memory first.";
    public const string TimeNotUnderstoodText = "Type a time such as 22:00 or 8:30.";
    public const string MemoryNotSavedText = "That change could not be saved. It applies until Desktop Rider is closed.";

    private readonly ISettingsService _settings;
    private readonly ConversationViewModel _conversation;
    private readonly RiderMemory _memory;
    private readonly ILogger<RiderSettingsViewModel> _logger;
    private string _problem = string.Empty;
    private string _conversationMessage = string.Empty;
    private string _memoryMessage = string.Empty;
    private string _newNote = string.Empty;
    private string _quietHoursMessage = string.Empty;

    public RiderSettingsViewModel(
        ISettingsService settings,
        ConversationViewModel conversation,
        RiderMemory memory,
        ILogger<RiderSettingsViewModel>? logger = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _logger = logger ?? NullLogger<RiderSettingsViewModel>.Instance;
        _settings.Changed += OnSettingsChanged;
        _memory.Changed += OnMemoryChanged;
        ShowNotes();

        AddNoteCommand = new RelayCommand(() => _ = AddNoteAsync());
        ForgetEverythingCommand = new RelayCommand(() => _ = ForgetEverythingAsync());

        UseBuiltInPersonalityCommand = new RelayCommand(() => OwnPersonality = string.Empty);
        DeleteConversationCommand = new RelayCommand(() => _ = DeleteConversationAsync());
    }

    /// <summary>Puts the built-in rider back by removing the user's own description.</summary>
    public ICommand UseBuiltInPersonalityCommand { get; }

    public bool ToneCheeky
    {
        get => Ai.Tone == RiderTone.Cheeky;
        set => Choose(value, RiderTone.Cheeky);
    }

    public bool ToneFriendly
    {
        get => Ai.Tone == RiderTone.Friendly;
        set => Choose(value, RiderTone.Friendly);
    }

    public bool ToneSerious
    {
        get => Ai.Tone == RiderTone.Serious;
        set => Choose(value, RiderTone.Serious);
    }

    public bool ShortReplies
    {
        get => Ai.ReplyLength == ReplyLength.Brief;
        set => Choose(value, ReplyLength.Brief);
    }

    public bool DetailedReplies
    {
        get => Ai.ReplyLength == ReplyLength.Detailed;
        set => Choose(value, ReplyLength.Detailed);
    }

    /// <summary>What the rider calls the user. Empty means the rider uses no name.</summary>
    public string UserName
    {
        get => Ai.UserName ?? string.Empty;
        set
        {
            if (!SameText(value, Ai.UserName))
            {
                _ = ChangeAsync(s => s.Ai.UserName = value);
            }
        }
    }

    /// <summary>The user's own description of the rider. Empty means the built-in rider is used.</summary>
    public string OwnPersonality
    {
        get => Ai.SystemPrompt ?? string.Empty;
        set
        {
            if (!SameText(value, Ai.SystemPrompt))
            {
                _ = ChangeAsync(s => s.Ai.SystemPrompt = value);
            }
        }
    }

    /// <summary>True while the user's own description is in use, which is when the "how it talks" choices do nothing.</summary>
    public bool UsingOwnPersonality => Ai.SystemPrompt is not null;

    /// <summary>The opposite of <see cref="UsingOwnPersonality"/>, for switching the built-in choices on and off.</summary>
    public bool UsingBuiltInPersonality => !UsingOwnPersonality;

    /// <summary>Removes the conversation from the screen and from this computer.</summary>
    public ICommand DeleteConversationCommand { get; }

    /// <summary>Whether the conversation is kept on this computer between sessions.</summary>
    public bool KeepConversations
    {
        get => Memory.KeepConversationHistory;
        set
        {
            if (value != Memory.KeepConversationHistory)
            {
                _ = ChangeHistoryAsync(s => s.Memory.KeepConversationHistory = value);
            }
        }
    }

    public bool KeepOneWeek
    {
        get => Memory.RetentionDays == 7;
        set => ChooseRetention(value, 7);
    }

    public bool KeepOneMonth
    {
        get => Memory.RetentionDays == 30;
        set => ChooseRetention(value, 30);
    }

    public bool KeepThreeMonths
    {
        get => Memory.RetentionDays == 90;
        set => ChooseRetention(value, 90);
    }

    public bool KeepOneYear
    {
        get => Memory.RetentionDays == 365;
        set => ChooseRetention(value, 365);
    }

    /// <summary>Confirms that the conversation was deleted. Empty until the user deletes it.</summary>
    public string ConversationMessage
    {
        get => _conversationMessage;
        private set
        {
            if (_conversationMessage != value)
            {
                _conversationMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Removes the conversation from the screen and from this computer.</summary>
    public async Task DeleteConversationAsync()
    {
        await _conversation.ClearAsync();
        ConversationMessage = ConversationDeletedText;
    }

    /// <summary>Remembers what is typed in <see cref="NewNote"/>.</summary>
    public ICommand AddNoteCommand { get; }

    /// <summary>Deletes everything the rider remembers about the user.</summary>
    public ICommand ForgetEverythingCommand { get; }

    /// <summary>Everything the rider remembers about the user, oldest first.</summary>
    public ObservableCollection<MemoryNoteItem> Notes { get; } = [];

    public bool HasNotes => Notes.Count > 0;

    public bool HasNoNotes => Notes.Count == 0;

    /// <summary>Whether the rider uses, and adds to, what it remembers about the user.</summary>
    public bool RememberThings
    {
        get => Memory.Enabled;
        set
        {
            if (value != Memory.Enabled)
            {
                MemoryMessage = string.Empty;
                _ = ChangeAsync(s => s.Memory.Enabled = value);
            }
        }
    }

    /// <summary>Something the user is typing for the rider to remember.</summary>
    public string NewNote
    {
        get => _newNote;
        set
        {
            value ??= string.Empty;
            if (_newNote != value)
            {
                _newNote = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>What happened to the last change to the rider's memory, when there is something to say.</summary>
    public string MemoryMessage
    {
        get => _memoryMessage;
        private set
        {
            if (_memoryMessage != value)
            {
                _memoryMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Remembers what is typed in <see cref="NewNote"/> and empties the box.</summary>
    public async Task AddNoteAsync()
    {
        var result = await _memory.RememberAsync(NewNote);
        MemoryMessage = result switch
        {
            RememberResult.AlreadyRemembered => AlreadyRememberedText,
            RememberResult.Full => MemoryFullText,
            RememberResult.SwitchedOff => MemoryOffText,
            RememberResult.NotSaved => MemoryNotSavedText,
            _ => string.Empty,
        };
        if (result is RememberResult.Remembered or RememberResult.NotSaved or RememberResult.AlreadyRemembered)
        {
            NewNote = string.Empty;
        }
    }

    /// <summary>Deletes everything the rider remembers about the user.</summary>
    public async Task ForgetEverythingAsync() =>
        MemoryMessage = await _memory.ForgetEverythingAsync() ? EverythingForgottenText : MemoryNotSavedText;

    /// <summary>Whether the rider may start a conversation itself now and then. Off until the user switches it on.</summary>
    public bool RiderSpeaksFirst
    {
        get => Behaviour.ProactiveComments;
        set
        {
            if (value != Behaviour.ProactiveComments)
            {
                _ = ChangeAsync(s => s.Behaviour.ProactiveComments = value);
            }
        }
    }

    public bool Every15Minutes
    {
        get => Behaviour.ProactiveIntervalMinutes == 15;
        set => ChooseInterval(value, 15);
    }

    public bool Every30Minutes
    {
        get => Behaviour.ProactiveIntervalMinutes == 30;
        set => ChooseInterval(value, 30);
    }

    public bool EveryHour
    {
        get => Behaviour.ProactiveIntervalMinutes == 60;
        set => ChooseInterval(value, 60);
    }

    public bool EveryTwoHours
    {
        get => Behaviour.ProactiveIntervalMinutes == 120;
        set => ChooseInterval(value, 120);
    }

    /// <summary>Whether there are hours of the day when the rider never speaks first.</summary>
    public bool QuietHoursOn
    {
        get => Behaviour.QuietHoursEnabled;
        set
        {
            if (value != Behaviour.QuietHoursEnabled)
            {
                _ = ChangeAsync(s => s.Behaviour.QuietHoursEnabled = value);
            }
        }
    }

    /// <summary>When the quiet hours start, as typed: "22:00".</summary>
    public string QuietFrom
    {
        get => Show(Behaviour.QuietHoursStart);
        set => ChooseTime(value, Behaviour.QuietHoursStart, (s, time) => s.Behaviour.QuietHoursStart = time);
    }

    /// <summary>When the quiet hours end, as typed: "08:00".</summary>
    public string QuietUntil
    {
        get => Show(Behaviour.QuietHoursEnd);
        set => ChooseTime(value, Behaviour.QuietHoursEnd, (s, time) => s.Behaviour.QuietHoursEnd = time);
    }

    /// <summary>Help for a time that could not be understood. Empty when all is well.</summary>
    public string QuietHoursMessage
    {
        get => _quietHoursMessage;
        private set
        {
            if (_quietHoursMessage != value)
            {
                _quietHoursMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Reads a time of day as people type it: "22:00", "8.30", "8", "10pm". Null if it is not one.</summary>
    public static TimeOnly? ReadTime(string? typed)
    {
        var text = (typed ?? string.Empty).Trim().Replace('.', ':').ToUpperInvariant();
        if (text.Length == 0)
        {
            return null;
        }

        string[] formats = ["H:mm", "HH:mm", "h:mmtt", "h:mm tt", "htt", "h tt", "%H"];
        return TimeOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;
    }

    /// <summary>Words explaining that a change could not be saved. Empty when all is well.</summary>
    public string Problem
    {
        get => _problem;
        private set
        {
            if (_problem != value)
            {
                _problem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasProblem));
            }
        }
    }

    public bool HasProblem => _problem.Length > 0;

    private AiSettings Ai => _settings.Current.Ai;

    private MemorySettings Memory => _settings.Current.Memory;

    private BehaviourSettings Behaviour => _settings.Current.Behaviour;

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        _memory.Changed -= OnMemoryChanged;
    }

    private async Task ForgetAsync(MemoryNote note) =>
        MemoryMessage = await _memory.ForgetAsync(note) ? string.Empty : MemoryNotSavedText;

    private void OnMemoryChanged(object? sender, EventArgs e) => ShowNotes();

    private void ShowNotes()
    {
        Notes.Clear();
        foreach (var note in _memory.Notes)
        {
            Notes.Add(new MemoryNoteItem(note, new RelayCommand(() => _ = ForgetAsync(note))));
        }

        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HasNoNotes));
    }

    // A radio button reports both the one switched on and the one switched off; only the first is a choice.
    private void Choose(bool selected, RiderTone tone)
    {
        if (selected && Ai.Tone != tone)
        {
            _ = ChangeAsync(s => s.Ai.Tone = tone);
        }
    }

    private void Choose(bool selected, ReplyLength length)
    {
        if (selected && Ai.ReplyLength != length)
        {
            _ = ChangeAsync(s => s.Ai.ReplyLength = length);
        }
    }

    private static string Show(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private void ChooseInterval(bool selected, int minutes)
    {
        if (selected && Behaviour.ProactiveIntervalMinutes != minutes)
        {
            _ = ChangeAsync(s => s.Behaviour.ProactiveIntervalMinutes = minutes);
        }
    }

    private void ChooseTime(string typed, TimeOnly current, Action<AppSettings, TimeOnly> change)
    {
        if (ReadTime(typed) is not { } time)
        {
            // The box goes back to the time in use, with a hint about what to type.
            QuietHoursMessage = TimeNotUnderstoodText;
            OnAllPropertiesChanged();
            return;
        }

        QuietHoursMessage = string.Empty;
        if (time != current)
        {
            _ = ChangeAsync(s => change(s, time));
        }
        else
        {
            // "8" typed over "08:00" is the same time; the box is tidied to match.
            OnAllPropertiesChanged();
        }
    }

    private void ChooseRetention(bool selected, int days)
    {
        if (selected && Memory.RetentionDays != days)
        {
            _ = ChangeHistoryAsync(s => s.Memory.RetentionDays = days);
        }
    }

    // The saved conversation follows the choice at once: kept, trimmed to the new length of time, or removed.
    private async Task ChangeHistoryAsync(Action<AppSettings> change)
    {
        await ChangeAsync(change);
        ConversationMessage = string.Empty;
        await _conversation.ApplyHistoryChoiceAsync();
    }

    private async Task ChangeAsync(Action<AppSettings> change)
    {
        try
        {
            await _settings.UpdateAsync(change);
            Problem = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, ex);
            Problem = SaveFailedText;
            OnAllPropertiesChanged();
        }
    }

    private static bool SameText(string? typed, string? saved) =>
        string.Equals(typed?.Trim() ?? string.Empty, saved ?? string.Empty, StringComparison.Ordinal);

    private void OnSettingsChanged(object? sender, EventArgs e) => OnAllPropertiesChanged();

    [LoggerMessage(Level = LogLevel.Warning, Message = "A change to the rider's settings could not be saved.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);
}
