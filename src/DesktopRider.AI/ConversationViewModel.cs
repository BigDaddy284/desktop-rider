using System.Collections.ObjectModel;
using System.Windows.Input;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.AI;

/// <summary>Where a conversation is at this moment. The rider's animation follows it.</summary>
public enum ConversationState
{
    /// <summary>Nothing is happening; the user can type.</summary>
    Idle,

    /// <summary>A message has been sent and no words have come back yet.</summary>
    Thinking,

    /// <summary>The rider's reply is arriving.</summary>
    Talking,
}

/// <summary>Who a line of the conversation is from.</summary>
public enum Speaker
{
    User,
    Rider,

    /// <summary>Desktop Rider itself, explaining a problem. Never sent to the AI.</summary>
    Notice,
}

/// <summary>One line shown in the conversation. A reply's text grows while it arrives.</summary>
public sealed class ConversationLine(Speaker speaker, string text, DateTimeOffset saidAt = default) : ViewModelBase
{
    private string _text = text;

    public Speaker Speaker { get; } = speaker;

    /// <summary>When it was said, used to forget old conversations after the time the user chose.</summary>
    public DateTimeOffset SaidAt { get; } = saidAt;

    public string Text
    {
        get => _text;
        internal set
        {
            if (_text != value)
            {
                _text = value;
                OnPropertyChanged();
            }
        }
    }
}

/// <summary>Something the rider should do, asked for in conversation.</summary>
public sealed class RiderActionEventArgs(RiderAction action) : EventArgs
{
    public RiderAction Action { get; } = action;
}

/// <summary>Something Desktop Rider needs to tell the user during a conversation.</summary>
public sealed class ConversationNoticeEventArgs(string message) : EventArgs
{
    public string Message { get; } = message;
}

/// <summary>
/// The conversation with the rider: the lines said so far, what the user is typing, sending and stopping,
/// which AI model is in use and whether the AI can be reached. It has no window, so it is unit tested.
/// Nothing is sent to the AI until the user sends a message or opens the conversation.
/// While the user allows it, the conversation is also kept on this computer between sessions.
/// </summary>
/// <remarks>
/// Work continues on the thread that started it (no <c>ConfigureAwait(false)</c>) on purpose: the conversation
/// window shows <see cref="Lines"/> and <see cref="Models"/> directly, and Windows only allows those to change
/// on the interface thread.
/// </remarks>
public sealed partial class ConversationViewModel : ViewModelBase, IDisposable
{
    public const string NotCheckedText = "Not checked yet";
    public const string CheckingText = "Checking…";
    public const string ConnectedText = "Connected";
    public const string NotConnectedText = "Not connected";
    public const string NoModelText = "No AI model chosen yet";
    public const string ChooseModelAdvice = "Choose an AI model above to start talking.";
    public const string NoModelsAdvice = "Ollama is running but has no AI models yet. Add a model in Ollama, then press \"Check again\".";
    public const string ModelGoneAdvice = "The AI model you chose is no longer in Ollama. Choose another one above.";
    public const string EmptyReplyNotice = "The AI model answered, but with no words. Try again or choose another model.";
    public const string StoppedNotice = "Reply stopped.";
    public const string SaveFailedAdvice = "Desktop Rider could not save your choice of AI model. Please try again.";
    public const string ThinkingText = "The rider is thinking…";
    public const string TalkingText = "The rider is answering…";
    public const string RememberedNotice = "The rider will remember that. You can see or delete what it remembers in Rider settings.";
    public const string AlreadyRememberedNotice = "The rider already remembers that.";
    public const string MemoryOffNotice = "The rider's memory is switched off, so it will not remember that next time. You can switch it on in Rider settings.";
    public const string MemoryFullNotice = "The rider's memory is full. Delete something it remembers in Rider settings to make room.";
    public const string MemoryNotSavedNotice = "The rider will remember that until Desktop Rider closes, but it could not be saved for next time.";
    public const string HistoryNotSavedNotice = "Desktop Rider could not save this conversation, so it will be forgotten when Desktop Rider closes.";

    /// <summary>The most lines sent to the AI with each message, so a long chat does not slow every reply down.</summary>
    public const int MaxLinesSent = 30;

    /// <summary>The most lines kept on screen. The oldest are dropped first.</summary>
    public const int MaxLinesKept = 200;

    /// <summary>The longest message the user can send.</summary>
    public const int MaxMessageLength = 4000;

    private readonly IAiChatService _chat;
    private readonly ISettingsService _settings;
    private readonly ILogger<ConversationViewModel> _logger;
    private readonly IConversationStore? _store;
    private readonly TimeProvider _time;
    private readonly RiderMemory? _memory;
    private CancellationTokenSource? _reply;
    private bool _saveProblemTold;
    private ConversationState _state;
    private string _draft = string.Empty;
    private string _connectionText = NotCheckedText;
    private string _advice = string.Empty;
    private string _currentReply = string.Empty;
    private bool _connected;
    private bool _checking;
    private bool _disposed;

    public ConversationViewModel(
        IAiChatService chat,
        ISettingsService settings,
        ILogger<ConversationViewModel>? logger = null,
        IConversationStore? store = null,
        TimeProvider? timeProvider = null,
        RiderMemory? memory = null)
    {
        _memory = memory;
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? NullLogger<ConversationViewModel>.Instance;
        _store = store;
        _time = timeProvider ?? TimeProvider.System;
        _settings.Changed += OnSettingsChanged;

        SendCommand = new RelayCommand(() => _ = SendAsync());
        StopCommand = new RelayCommand(Stop);
        ClearCommand = new RelayCommand(Clear);
        CheckConnectionCommand = new RelayCommand(() => _ = CheckConnectionAsync());
    }

    /// <summary>Raised when Desktop Rider adds an explanation to the conversation, such as why the rider could not answer.</summary>
    public event EventHandler<ConversationNoticeEventArgs>? NoticeAdded;

    /// <summary>
    /// Raised when the rider should do something, such as a wheelie: the AI asked for it with a tag in its reply,
    /// or the user's message asked for it in plain words. Raised at most once per action for each message sent.
    /// </summary>
    public event EventHandler<RiderActionEventArgs>? ActionRequested;

    /// <summary>Everything said so far, oldest first.</summary>
    public ObservableCollection<ConversationLine> Lines { get; } = [];

    /// <summary>The AI models found at the last connection check.</summary>
    public ObservableCollection<string> Models { get; } = [];

    public ICommand SendCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand ClearCommand { get; }

    public ICommand CheckConnectionCommand { get; }

    /// <summary>What the user is typing.</summary>
    public string Draft
    {
        get => _draft;
        set
        {
            value ??= string.Empty;
            if (_draft != value)
            {
                _draft = value;
                OnPropertyChanged();
            }
        }
    }

    public ConversationState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(ProcessingText));
            }
        }
    }

    /// <summary>True from sending a message until the reply has finished, failed or been stopped.</summary>
    public bool IsBusy => _state != ConversationState.Idle;

    /// <summary>Words for the "AI is working" indicator. Empty when it is not.</summary>
    public string ProcessingText => _state switch
    {
        ConversationState.Thinking => ThinkingText,
        ConversationState.Talking => TalkingText,
        _ => string.Empty,
    };

    /// <summary>The rider's reply that is arriving, or the last one that arrived. Shown in the speech bubble.</summary>
    public string CurrentReply
    {
        get => _currentReply;
        private set
        {
            if (_currentReply != value)
            {
                _currentReply = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The model the user chose, or null. Setting it remembers the choice.</summary>
    public string? SelectedModel
    {
        get => _settings.Current.Ai.Model;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, _settings.Current.Ai.Model, StringComparison.Ordinal))
            {
                _ = SelectModelAsync(value);
            }
        }
    }

    /// <summary>The current model, in words.</summary>
    public string ModelText => string.IsNullOrWhiteSpace(_settings.Current.Ai.Model) ? NoModelText : _settings.Current.Ai.Model;

    /// <summary>Whether the AI could be reached the last time it was tried, in words.</summary>
    public string ConnectionText
    {
        get => _connectionText;
        private set
        {
            if (_connectionText != value)
            {
                _connectionText = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>True when the AI answered the last time it was tried.</summary>
    public bool IsConnected
    {
        get => _connected;
        private set
        {
            if (_connected != value)
            {
                _connected = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>What the user should do to get the AI working. Empty when nothing needs doing.</summary>
    public string Advice
    {
        get => _advice;
        private set
        {
            if (_advice != value)
            {
                _advice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAdvice));
            }
        }
    }

    public bool HasAdvice => _advice.Length > 0;

    /// <summary>
    /// Sends what the user typed and shows the rider's reply as it arrives. Does nothing if the box is empty
    /// or a reply is already on its way.
    /// </summary>
    public async Task SendAsync()
    {
        var text = Draft.Trim();
        if (text.Length == 0 || IsBusy || _disposed)
        {
            return;
        }

        if (text.Length > MaxMessageLength)
        {
            text = text[..MaxMessageLength];
        }

        Draft = string.Empty;
        Add(new ConversationLine(Speaker.User, text, _time.GetUtcNow()));
        var history = BuildHistory();

        // Kept straight away, so the message survives even if Desktop Rider is closed before the answer ends.
        var saved = SaveHistoryAsync();

        var stop = new CancellationTokenSource();
        _reply = stop;
        CurrentReply = string.Empty;
        State = ConversationState.Thinking;
        ConversationLine? answer = null;
        var tags = new ActionTagFilter();
        var done = new HashSet<RiderAction>();
        var asked = RiderActions.FromRequest(text);
        try
        {
            // "Remember that…" is kept before the message goes to the AI, so the rider's answer already knows it.
            await RememberIfAskedAsync(text);
            if (!ReferenceEquals(_reply, stop))
            {
                return;
            }

            await foreach (var piece in _chat.StreamReplyAsync(history, stop.Token))
            {
                if (!ReferenceEquals(_reply, stop))
                {
                    // The conversation was cleared while this reply was arriving.
                    break;
                }

                // Tags such as [wheelie] make the rider move and are never shown.
                var (words, actions) = tags.Add(piece);
                foreach (var action in actions)
                {
                    Perform(action, done);
                }

                var addition = answer is null ? words.TrimStart() : words;
                if (addition.Length == 0)
                {
                    continue;
                }

                if (answer is null)
                {
                    answer = new ConversationLine(Speaker.Rider, addition, _time.GetUtcNow());
                    Add(answer);

                    // The words are in place before the state changes, so nothing ever shows an empty reply.
                    CurrentReply = answer.Text;
                    State = ConversationState.Talking;

                    // Small models often forget the tag, so what the user plainly asked for is done as the answer starts.
                    foreach (var action in asked)
                    {
                        Perform(action, done);
                    }
                }
                else
                {
                    answer.Text += addition;
                }

                CurrentReply = answer.Text;
            }

            if (!ReferenceEquals(_reply, stop))
            {
                return;
            }

            // Brackets still held back at the end were not a tag, so they belong to the reply.
            var rest = tags.Finish();
            if (answer is not null && rest.Length > 0)
            {
                answer.Text += rest;
            }

            IsConnected = true;
            ConnectionText = ConnectedText;
            if (answer is null)
            {
                Notify(EmptyReplyNotice);
            }
            else
            {
                answer.Text = answer.Text.TrimEnd();
                CurrentReply = answer.Text;
            }
        }
        catch (AiProviderException problem)
        {
            if (ReferenceEquals(_reply, stop))
            {
                LogReplyFailed(_logger, problem.Kind);
                ApplyProblem(problem);
                Notify(problem.Message);
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            if (ReferenceEquals(_reply, stop))
            {
                Notify(StoppedNotice);
            }
        }
        catch (OperationCanceledException)
        {
            // Desktop Rider is closing; there is nobody to tell.
        }
        catch (ObjectDisposedException)
        {
            // As above.
        }
        finally
        {
            var current = ReferenceEquals(_reply, stop);
            if (current)
            {
                _reply = null;
                State = ConversationState.Idle;
            }

            stop.Dispose();
            if (current && !_disposed)
            {
                await saved;
                await SaveHistoryAsync();
            }
        }
    }

    /// <summary>Stops a reply that is still arriving. What has arrived so far is kept.</summary>
    public void Stop() => _reply?.Cancel();

    /// <summary>
    /// Empties the conversation, stopping any reply that is still arriving, and removes the saved copy from
    /// this computer so that it does not come back next time.
    /// </summary>
    public void Clear() => _ = ClearAsync();

    /// <summary>As <see cref="Clear"/>, finishing once the saved copy has gone.</summary>
    public async Task ClearAsync()
    {
        var reply = _reply;
        _reply = null;
        reply?.Cancel();
        Lines.Clear();
        State = ConversationState.Idle;
        CurrentReply = string.Empty;
        await ForgetHistoryAsync();
    }

    /// <summary>
    /// Brings back the conversation kept from last time. Call once at start-up, after the settings are ready.
    /// Lines older than the time the user chose to keep them are left out. Contacts nothing.
    /// </summary>
    public async Task RestoreAsync()
    {
        if (_store is null || !_settings.Current.Memory.KeepConversationHistory || _disposed)
        {
            return;
        }

        try
        {
            var kept = await _store.LoadAsync();
            var oldest = OldestKept();
            foreach (var line in kept.Where(line => line.At >= oldest).TakeLast(MaxLinesKept))
            {
                Lines.Add(new ConversationLine(line.From, line.Text, line.At));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or ObjectDisposedException)
        {
            LogHistoryNotLoaded(_logger, ex);
        }
    }

    /// <summary>
    /// Carries out the user's choice of whether conversations are kept, after they change it: switching it on
    /// keeps the conversation so far, switching it off removes the saved copy from this computer.
    /// </summary>
    public Task ApplyHistoryChoiceAsync() =>
        _settings.Current.Memory.KeepConversationHistory ? SaveHistoryAsync() : ForgetHistoryAsync();

    /// <summary>
    /// Finds out whether the AI can be reached and which models it has. Called when the user opens the conversation
    /// or presses "Check again"; never in the background.
    /// </summary>
    public async Task CheckConnectionAsync()
    {
        if (_checking || _disposed)
        {
            return;
        }

        _checking = true;
        ConnectionText = CheckingText;
        try
        {
            var report = await _chat.CheckConnectionAsync();
            ReplaceModels(report.Models.Select(model => model.Name));
            if (report.Problem is { } problem)
            {
                IsConnected = false;
                ConnectionText = NotConnectedText;
                Advice = problem.Message;
                return;
            }

            IsConnected = true;
            ConnectionText = ConnectedText;
            Advice = report switch
            {
                { Models.Count: 0 } => NoModelsAdvice,
                { SelectedModelAvailable: true } => string.Empty,
                { SelectedModel: null or "" } => ChooseModelAdvice,
                _ => ModelGoneAdvice,
            };
        }
        catch (OperationCanceledException)
        {
            // Desktop Rider is closing; there is nobody to tell.
        }
        catch (ObjectDisposedException)
        {
            // As above.
        }
        finally
        {
            _checking = false;
            if (ConnectionText == CheckingText)
            {
                ConnectionText = NotCheckedText;
            }
        }
    }

    /// <summary>Remembers the user's choice of AI model.</summary>
    public async Task SelectModelAsync(string model)
    {
        try
        {
            await _chat.SelectModelAsync(model);
            if (Advice is ChooseModelAdvice or ModelGoneAdvice or SaveFailedAdvice)
            {
                Advice = string.Empty;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, ex);
            Advice = SaveFailedAdvice;
            OnPropertyChanged(nameof(SelectedModel));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        Stop();
    }

    // Explanations from Desktop Rider and empty lines are not part of what was said, so the AI never sees them.
    private List<ChatMessage> BuildHistory() =>
        Lines
            .Where(line => line.Speaker != Speaker.Notice && line.Text.Length > 0)
            .TakeLast(MaxLinesSent)
            .Select(line => new ChatMessage(line.Speaker == Speaker.User ? ChatRole.User : ChatRole.Assistant, line.Text))
            .ToList();

    private DateTimeOffset OldestKept() => _time.GetUtcNow().AddDays(-_settings.Current.Memory.RetentionDays);

    // Explanations from Desktop Rider are not part of the conversation, so they are not kept either.
    private async Task SaveHistoryAsync()
    {
        if (_store is null || !_settings.Current.Memory.KeepConversationHistory)
        {
            return;
        }

        var oldest = OldestKept();
        var lines = Lines
            .Where(line => line.Speaker != Speaker.Notice && line.Text.Length > 0 && line.SaidAt >= oldest)
            .Select(line => new SavedLine(line.Speaker, line.Text, line.SaidAt))
            .ToList();
        try
        {
            await _store.SaveAsync(lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogHistoryNotSaved(_logger, ex);
            if (!_saveProblemTold && !_disposed)
            {
                // Said once, not after every message.
                _saveProblemTold = true;
                Notify(HistoryNotSavedNotice);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Desktop Rider is closing.
        }
    }

    private async Task ForgetHistoryAsync()
    {
        if (_store is null)
        {
            return;
        }

        try
        {
            await _store.DeleteAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogHistoryNotSaved(_logger, ex);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Desktop Rider is closing.
        }
    }

    // The outcome is written into the conversation by Desktop Rider itself, so the user can trust that it happened
    // (or did not) whatever the AI goes on to say. It is not shown in the speech bubble, which is for the rider's answer.
    private async Task RememberIfAskedAsync(string message)
    {
        if (_memory is null || RiderMemory.FactToRemember(message) is not { } fact)
        {
            return;
        }

        var notice = await _memory.RememberAsync(fact) switch
        {
            RememberResult.Remembered => RememberedNotice,
            RememberResult.AlreadyRemembered => AlreadyRememberedNotice,
            RememberResult.SwitchedOff => MemoryOffNotice,
            RememberResult.Full => MemoryFullNotice,
            RememberResult.NotSaved => MemoryNotSavedNotice,
            _ => null,
        };
        if (notice is not null)
        {
            Add(new ConversationLine(Speaker.Notice, notice, _time.GetUtcNow()));
        }
    }

    private void Add(ConversationLine line)
    {
        Lines.Add(line);
        while (Lines.Count > MaxLinesKept)
        {
            Lines.RemoveAt(0);
        }
    }

    private void Perform(RiderAction action, HashSet<RiderAction> done)
    {
        if (done.Add(action))
        {
            ActionRequested?.Invoke(this, new RiderActionEventArgs(action));
        }
    }

    private void Notify(string message)
    {
        Add(new ConversationLine(Speaker.Notice, message, _time.GetUtcNow()));
        NoticeAdded?.Invoke(this, new ConversationNoticeEventArgs(message));
    }

    private void ApplyProblem(AiProviderException problem)
    {
        switch (problem.Kind)
        {
            case AiErrorKind.NotRunning or AiErrorKind.Timeout or AiErrorKind.ConnectionLost:
                IsConnected = false;
                ConnectionText = NotConnectedText;
                break;
            case AiErrorKind.NoModelSelected:
                Advice = ChooseModelAdvice;
                break;
            case AiErrorKind.ModelNotFound:
                Advice = ModelGoneAdvice;
                break;
            default:
                break;
        }
    }

    private void ReplaceModels(IEnumerable<string> names)
    {
        var wanted = names.ToList();
        if (!wanted.SequenceEqual(Models, StringComparer.Ordinal))
        {
            Models.Clear();
            foreach (var name in wanted)
            {
                Models.Add(name);
            }
        }

        // The list has just been rebuilt, so the window needs telling which entry is the chosen one.
        OnPropertyChanged(nameof(SelectedModel));
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(SelectedModel));
        OnPropertyChanged(nameof(ModelText));
    }

    // The words of the conversation are never logged, only what kind of problem stopped a reply.
    [LoggerMessage(Level = LogLevel.Information, Message = "The rider could not answer: {Kind}.")]
    private static partial void LogReplyFailed(ILogger logger, AiErrorKind kind);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The conversation could not be saved on this computer.")]
    private static partial void LogHistoryNotSaved(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The saved conversation could not be read. The rider starts afresh.")]
    private static partial void LogHistoryNotLoaded(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The choice of AI model could not be saved.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);
}
