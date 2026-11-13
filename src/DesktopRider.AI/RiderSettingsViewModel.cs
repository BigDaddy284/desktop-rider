using System.Windows.Input;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.AI;

/// <summary>
/// The rider's settings: its personality and what it keeps between sessions. Every choice takes effect and is saved the moment it is made, so there
/// is no Save button to forget. It has no window, so it is unit tested.
/// </summary>
public sealed partial class RiderSettingsViewModel : ViewModelBase, IDisposable
{
    public const string SaveFailedText = "Desktop Rider could not save that change. It applies until Desktop Rider is closed.";

    public const string ConversationDeletedText = "The conversation has been deleted.";

    private readonly ISettingsService _settings;
    private readonly ConversationViewModel _conversation;
    private readonly ILogger<RiderSettingsViewModel> _logger;
    private string _problem = string.Empty;
    private string _conversationMessage = string.Empty;

    public RiderSettingsViewModel(ISettingsService settings, ConversationViewModel conversation, ILogger<RiderSettingsViewModel>? logger = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _conversation = conversation ?? throw new ArgumentNullException(nameof(conversation));
        _logger = logger ?? NullLogger<RiderSettingsViewModel>.Instance;
        _settings.Changed += OnSettingsChanged;

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

    public void Dispose() => _settings.Changed -= OnSettingsChanged;

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
