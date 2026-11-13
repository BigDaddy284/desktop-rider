#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DesktopRider.Core.Logging;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.AI
{
    /// <summary>Something to tell the user about the AI: a short heading and a sentence or two.</summary>
    public sealed class AiMessageEventArgs : EventArgs
    {
        public AiMessageEventArgs(string title, string message)
        {
            Title = title;
            Message = message;
        }

        public string Title { get; }

        public string Message { get; }
    }

    /// <summary>
    /// The AI lines of the notification-area menu: talk to the rider, check the connection, choose a model and open the rider's settings.
    /// Nothing is sent to the AI until the user picks one of these.
    /// Every choice is answered straight away with a message, then with its result, so a click never appears to do nothing.
    /// </summary>
    public sealed partial class AiMenuViewModel : ITrayMenuContributor
    {
        public const string CheckText = "Check the AI connection";
        public const string CheckingText = "Checking the AI connection…";
        public const string ChooseModelText = "Choose an AI model";
        public const string TalkText = "Talk to the rider";
        public const string SettingsText = "Rider settings";

        /// <summary>Keeps a runaway reply from filling the screen.</summary>
        public const int MaxMessageLength = 1200;

        private readonly IAiChatService _chat;
        private readonly ILogger<AiMenuViewModel> _logger;
        private readonly Func<string?> _selectedModelSource;
        private readonly object _gate = new();
        private IReadOnlyList<AiModelInfo> _models = Array.Empty<AiModelInfo>();
        private bool _checking;

        public AiMenuViewModel(IAiChatService chat, Core.Configuration.ISettingsService settings, ILogger<AiMenuViewModel>? logger = null)
        {
            _chat = chat ?? throw new ArgumentNullException(nameof(chat));
            Guard.NotNull(settings, nameof(settings));
            _logger = logger ?? NullLogger<AiMenuViewModel>.Instance;
            _selectedModelSource = () => settings.Current.Ai.Model;
        }

        /// <summary>Raised with the outcome of something the user asked for.</summary>
        public event EventHandler<AiMessageEventArgs>? MessageRequested;

        /// <summary>Raised while something the user asked for is under way, at once when it starts.</summary>
        public event EventHandler<AiMessageEventArgs>? ProgressChanged;

        /// <summary>Raised when the user chooses to talk to the rider.</summary>
        public event EventHandler? TalkRequested;

        /// <summary>Raised when the user chooses to open the rider's settings.</summary>
        public event EventHandler? SettingsRequested;

        public IReadOnlyList<TrayMenuEntry> BuildEntries()
        {
            lock (_gate)
            {
                var selectedModel = _selectedModelSource();
                var entries = new List<TrayMenuEntry>
                {
                    new(TalkText, new RelayCommand(() => TalkRequested?.Invoke(this, EventArgs.Empty))),
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

                entries.Add(new TrayMenuEntry(SettingsText, new RelayCommand(() => SettingsRequested?.Invoke(this, EventArgs.Empty))));

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
                Tell("AI model chosen", $"The rider will use {model}. Click the rider, or choose \"{TalkText}\" from this menu, to start talking.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogSaveFailed(_logger, ex);
                Tell("The choice could not be saved", "Desktop Rider could not save your choice of AI model. Please try again.");
            }
        }

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
        private static void LogOutcome(ILogger logger, string title)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.Log(LogLevel.Information, $"AI menu outcome: {LogText.Of(title)}.", null);
            }
        }

        private static void LogSaveFailed(ILogger logger, Exception exception)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.Log(LogLevel.Warning, "The choice of AI model could not be saved.", exception);
            }
        }
    }
}
