using System.Text;
using DesktopRider.Core.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.AI;

/// <summary>Something to tell the user about the AI: a short heading and a sentence or two.</summary>
public sealed class AiMessageEventArgs(string title, string message) : EventArgs
{
    public string Title { get; } = title;

    public string Message { get; } = message;
}

/// <summary>
/// The AI lines of the notification-area menu: check the connection, choose a model, and ask the rider for a
/// short hello to prove the whole chain works. Nothing is sent to the AI until the user picks one of these.
/// Every choice is answered straight away with a message, then with its result, so a click never appears to do nothing.
/// The hello line is a stand-in until the conversation window arrives.
/// </summary>
public sealed partial class AiMenuViewModel : ITrayMenuContributor, IDisposable
{
    public const string CheckText = "Check the AI connection";
    public const string CheckingText = "Checking the AI connection…";
    public const string ChooseModelText = "Choose an AI model";
    public const string SayHelloText = "Ask the rider to say hello";
    public const string StopReplyText = "Stop the rider's reply";
    public const string HelloPrompt = "Say hello to me in one short sentence.";

    /// <summary>Keeps a runaway reply from filling the screen.</summary>
    public const int MaxMessageLength = 1200;

    private readonly IAiChatService _chat;
    private readonly ILogger<AiMenuViewModel> _logger;
    private readonly Func<string?> _selectedModelSource;
    private readonly Lock _gate = new();
    private IReadOnlyList<AiModelInfo> _models = [];
    private bool _checking;
    private CancellationTokenSource? _reply;

    public AiMenuViewModel(IAiChatService chat, Core.Configuration.ISettingsService settings, ILogger<AiMenuViewModel>? logger = null)
    {
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        ArgumentNullException.ThrowIfNull(settings);
        _logger = logger ?? NullLogger<AiMenuViewModel>.Instance;
        _selectedModelSource = () => settings.Current.Ai.Model;
    }

    /// <summary>Raised with the outcome of something the user asked for.</summary>
    public event EventHandler<AiMessageEventArgs>? MessageRequested;

    /// <summary>Raised while something the user asked for is under way: at once when it starts, and as a reply grows.</summary>
    public event EventHandler<AiMessageEventArgs>? ProgressChanged;

    public IReadOnlyList<TrayMenuEntry> BuildEntries()
    {
        lock (_gate)
        {
            var selectedModel = _selectedModelSource();
            var entries = new List<TrayMenuEntry>
            {
                new(_checking ? CheckingText : CheckText, new RelayCommand(() => _ = CheckConnectionAsync())),
            };
            if (_models.Count > 0)
            {
                var choices = _models
                    .Select(model => new TrayMenuEntry(
                        model.Details is null ? model.Name : $"{model.Name} ({model.Details})",
                        new RelayCommand(() => _ = SelectModelAsync(model.Name)),
                        string.Equals(model.Name, selectedModel, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                entries.Add(new TrayMenuEntry(ChooseModelText, null, Children: choices));
            }

            if (_reply is not null)
            {
                entries.Add(new TrayMenuEntry(StopReplyText, new RelayCommand(StopReply)));
            }
            else if (selectedModel is not null)
            {
                entries.Add(new TrayMenuEntry(SayHelloText, new RelayCommand(() => _ = SayHelloAsync())));
            }

            return entries;
        }
    }

    /// <summary>Tests the connection, remembers the models found for the menu, and tells the user the result.</summary>
    public async Task CheckConnectionAsync()
    {
        lock (_gate)
        {
            if (_checking)
            {
                return;
            }

            _checking = true;
        }

        try
        {
            Progress("Checking the AI connection", "Looking for Ollama. This takes a few seconds at most.");
            var report = await _chat.CheckConnectionAsync().ConfigureAwait(false);
            lock (_gate)
            {
                _models = report.Models;
            }

            Tell(Describe(report));
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
            lock (_gate)
            {
                _checking = false;
            }
        }
    }

    public async Task SelectModelAsync(string model)
    {
        try
        {
            await _chat.SelectModelAsync(model).ConfigureAwait(false);
            Tell("AI model chosen", $"The rider will use {model}. Choose \"{SayHelloText}\" from this menu to try it.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogSaveFailed(_logger, ex);
            Tell("The choice could not be saved", "Desktop Rider could not save your choice of AI model. Please try again.");
        }
    }

    /// <summary>Asks the chosen model for a short greeting and shows it, to prove that replies arrive.</summary>
    public async Task SayHelloAsync()
    {
        var stop = new CancellationTokenSource();
        lock (_gate)
        {
            if (_reply is not null)
            {
                stop.Dispose();
                return;
            }

            _reply = stop;
        }

        var reply = new StringBuilder();
        try
        {
            Progress("Asking the rider", $"Waiting for the AI model. The first reply can take a minute while Ollama loads it. To give up, choose \"{StopReplyText}\" from the icon's menu.");
            await foreach (var piece in _chat.StreamReplyAsync([new ChatMessage(ChatRole.User, HelloPrompt)], stop.Token).ConfigureAwait(false))
            {
                reply.Append(piece);
                Progress("The rider is answering", reply.ToString().TrimStart());
            }

            var text = reply.ToString().Trim();
            Tell("The rider says", text.Length == 0 ? "The AI model answered, but with no words. Try again or choose another model." : text);
        }
        catch (AiProviderException problem)
        {
            Tell("The rider could not answer", problem.Message);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            Tell("Reply stopped", "The rider's reply was stopped.");
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
            lock (_gate)
            {
                _reply = null;
            }

            stop.Dispose();
        }
    }

    /// <summary>Stops a reply that is still being produced.</summary>
    public void StopReply()
    {
        lock (_gate)
        {
            _reply?.Cancel();
        }
    }

    public void Dispose() => StopReply();

    private static (string Title, string Message) Describe(AiConnectionReport report)
    {
        if (report.Problem is { } problem)
        {
            return ("The AI is not connected", problem.Message);
        }

        if (report.Models.Count == 0)
        {
            return ("Ollama is connected", "Ollama is running but has no AI models yet. Add a model in Ollama, then check again.");
        }

        var found = report.Models.Count == 1 ? "Found 1 AI model" : $"Found {report.Models.Count} AI models";
        if (report.SelectedModelAvailable)
        {
            return ("Ollama is connected", $"{found}. The rider is using {report.SelectedModel}.");
        }

        return report.SelectedModel is null
            ? ("Ollama is connected", $"{found}. Open this menu again and pick one under \"{ChooseModelText}\".")
            : ("Ollama is connected", $"{found}, but {report.SelectedModel} is no longer one of them. Pick another under \"{ChooseModelText}\".");
    }

    private void Tell((string Title, string Message) hint) => Tell(hint.Title, hint.Message);

    private void Tell(string title, string message)
    {
        LogOutcome(_logger, title);
        MessageRequested?.Invoke(this, new AiMessageEventArgs(title, Shorten(message)));
    }

    private void Progress(string title, string message) =>
        ProgressChanged?.Invoke(this, new AiMessageEventArgs(title, Shorten(message)));

    private static string Shorten(string message) =>
        message.Length > MaxMessageLength ? message[..(MaxMessageLength - 1)].TrimEnd() + "…" : message;

    // Only the heading is logged. The message may contain the rider's reply, which stays out of the log.
    [LoggerMessage(Level = LogLevel.Information, Message = "AI menu outcome: {Title}.")]
    private static partial void LogOutcome(ILogger logger, string title);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The choice of AI model could not be saved.")]
    private static partial void LogSaveFailed(ILogger logger, Exception exception);
}
