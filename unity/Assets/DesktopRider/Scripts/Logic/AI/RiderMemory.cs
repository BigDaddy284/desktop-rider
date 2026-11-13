#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.Logging;

namespace DesktopRider.AI
{
    /// <summary>One thing the user asked the rider to remember.</summary>
    public sealed record MemoryNote(string Text, DateTimeOffset SavedAt);

    /// <summary>Keeps what the rider remembers between sessions. Everything stays on this computer.</summary>
    public interface IMemoryStore
    {
        /// <summary>The saved notes, oldest first. Never throws for a missing or damaged file; it is simply empty.</summary>
        Task<IReadOnlyList<MemoryNote>> LoadAsync(CancellationToken cancellationToken = default);

        Task SaveAsync(IReadOnlyList<MemoryNote> notes, CancellationToken cancellationToken = default);

        /// <summary>Removes everything the rider remembers from this computer.</summary>
        Task DeleteAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Stores what the rider remembers as a readable JSON file in the user's local data folder.</summary>
    public sealed partial class JsonMemoryStore : IMemoryStore, IDisposable
    {
        private readonly LocalJsonFile<SavedMemory> _file;
        private readonly ILogger<JsonMemoryStore> _logger;

        public JsonMemoryStore(AppPaths paths, ILogger<JsonMemoryStore>? logger = null, TimeProvider? timeProvider = null)
        {
            Guard.NotNull(paths, nameof(paths));
            _file = new LocalJsonFile<SavedMemory>(paths.MemoryFile, timeProvider);
            _logger = logger ?? NullLogger<JsonMemoryStore>.Instance;
        }

        public async Task<IReadOnlyList<MemoryNote>> LoadAsync(CancellationToken cancellationToken = default)
        {
            var read = await _file.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (read.Status == LocalFileStatus.Damaged)
            {
                LogDamagedFile(_logger, read.Problem, read.BackupFile);
            }

            return (read.Value?.Notes ?? new List<MemoryNote>()).Where(note => !string.IsNullOrWhiteSpace(note?.Text)).ToList();
        }

        public Task SaveAsync(IReadOnlyList<MemoryNote> notes, CancellationToken cancellationToken = default)
        {
            Guard.NotNull(notes, nameof(notes));
            return _file.WriteAsync(new SavedMemory { Notes = notes.ToList() }, cancellationToken);
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default) => _file.DeleteAsync(cancellationToken);

        public void Dispose() => _file.Dispose();

        // Only the fact is logged. What the rider remembers never goes in the log.
        private static void LogDamagedFile(ILogger logger, Exception? exception, string? backupFile)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.Log(LogLevel.Warning, $"The rider's saved memory could not be read, so it starts empty. Backup of the damaged file: {LogText.Of(backupFile)}", exception);
            }
        }

        /// <summary>The shape of the file.</summary>
        internal sealed class SavedMemory
        {
            public int Version { get; set; } = 1;

            public List<MemoryNote> Notes { get; set; } = new();
        }
    }

    /// <summary>What happened when the rider was asked to remember something.</summary>
    public enum RememberResult
    {
        /// <summary>It is now remembered and saved.</summary>
        Remembered,

        /// <summary>The rider already remembers exactly this.</summary>
        AlreadyRemembered,

        /// <summary>There was nothing to remember.</summary>
        Nothing,

        /// <summary>The user has the rider's memory switched off.</summary>
        SwitchedOff,

        /// <summary>The rider already remembers as many things as it can hold.</summary>
        Full,

        /// <summary>It is remembered for now, but could not be saved for next time.</summary>
        NotSaved,
    }

    /// <summary>
    /// What the rider remembers about the user: short notes the user asked it to keep, either by saying
    /// "remember that…" in conversation or by typing them in the rider's settings. Nothing is ever added without
    /// the user asking, the notes never leave this computer except as part of a message to the user's own AI,
    /// and the user can see, delete or switch off all of it.
    /// </summary>
    public sealed partial class RiderMemory
    {
        /// <summary>The most notes kept, so the instructions sent to the AI stay a sensible size.</summary>
        public const int MaxNotes = 50;

        /// <summary>The longest a note can be.</summary>
        public const int MaxNoteLength = 200;

        private static readonly string[] NotFacts =
        {
            "when", "what", "whats", "how", "why", "who", "where", "whether", "if", "to", "me", "us", "this", "that", "it",
            "those", "these", "anything", "everything", "something", "nothing", "time", "the time", "last time",
        };

        private readonly IMemoryStore _store;
        private readonly ISettingsService _settings;
        private readonly TimeProvider _time;
        private readonly ILogger<RiderMemory> _logger;
        private volatile IReadOnlyList<MemoryNote> _notes = Array.Empty<MemoryNote>();

        public RiderMemory(IMemoryStore store, ISettingsService settings, TimeProvider? timeProvider = null, ILogger<RiderMemory>? logger = null)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _time = timeProvider ?? TimeProvider.System;
            _logger = logger ?? NullLogger<RiderMemory>.Instance;
        }

        /// <summary>Raised when the notes change.</summary>
        public event EventHandler? Changed;

        /// <summary>True while the user has the rider's memory switched on.</summary>
        public bool Enabled => _settings.Current.Memory.Enabled;

        /// <summary>Everything remembered, oldest first, whether or not memory is switched on.</summary>
        public IReadOnlyList<MemoryNote> Notes => _notes;

        /// <summary>What the AI is told about the user: the notes while memory is switched on, otherwise nothing.</summary>
        public IReadOnlyList<string> ForConversation() => Enabled ? _notes.Select(note => note.Text).ToList() : Array.Empty<string>();

        /// <summary>
        /// The fact in a message that asks the rider to remember something, such as "remember that my bike is a 250".
        /// Null for anything else, including questions like "do you remember…?" and reminders like "remember to…".
        /// </summary>
        public static string? FactToRemember(string message)
        {
            Guard.NotNull(message, nameof(message));
            var text = message.Trim();
            if (text.EndsWith('?'))
            {
                return null;
            }

            var match = RememberRequest().Match(text);
            if (!match.Success)
            {
                return null;
            }

            var fact = Tidy(match.Groups["fact"].Value);
            var start = Apostrophes().Replace(fact.ToLowerInvariant(), string.Empty);
            if (fact.Length < 3 || NotFacts.Any(word => start == word || start.StartsWith(word + " ", StringComparison.Ordinal)))
            {
                return null;
            }

            return fact;
        }

        /// <summary>Reads the saved notes. Call once at start-up. Contacts nothing.</summary>
        public async Task LoadAsync()
        {
            try
            {
                var loaded = await _store.LoadAsync();
                _notes = loaded.Select(note => note with { Text = Tidy(note.Text) }).Where(note => note.Text.Length > 0).TakeLast(MaxNotes).ToList();
                Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException or ObjectDisposedException)
            {
                LogNotLoaded(_logger, ex);
            }
        }

        /// <summary>Remembers something, if memory is switched on and there is room.</summary>
        public async Task<RememberResult> RememberAsync(string text)
        {
            var note = Tidy(text ?? string.Empty);
            if (note.Length == 0)
            {
                return RememberResult.Nothing;
            }

            if (!Enabled)
            {
                return RememberResult.SwitchedOff;
            }

            var notes = _notes;
            if (notes.Any(existing => string.Equals(existing.Text, note, StringComparison.OrdinalIgnoreCase)))
            {
                return RememberResult.AlreadyRemembered;
            }

            if (notes.Count >= MaxNotes)
            {
                return RememberResult.Full;
            }

            return await ReplaceAsync(notes.Append(new MemoryNote(note, _time.GetUtcNow())).ToList()) ? RememberResult.Remembered : RememberResult.NotSaved;
        }

        /// <summary>Forgets one thing. Returns false if it could not be removed from the saved file.</summary>
        public Task<bool> ForgetAsync(MemoryNote note)
        {
            Guard.NotNull(note, nameof(note));
            var notes = _notes;
            return notes.Contains(note) ? ReplaceAsync(notes.Where(existing => existing != note).ToList()) : Task.FromResult(true);
        }

        /// <summary>Forgets everything and removes the saved file. Returns false if the file could not be removed.</summary>
        public async Task<bool> ForgetEverythingAsync()
        {
            _notes = Array.Empty<MemoryNote>();
            Changed?.Invoke(this, EventArgs.Empty);
            try
            {
                await _store.DeleteAsync();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogNotSaved(_logger, ex);
                return false;
            }
        }

        // The change applies at once; saving follows. If saving fails the change still holds until Desktop Rider closes.
        private async Task<bool> ReplaceAsync(IReadOnlyList<MemoryNote> notes)
        {
            _notes = notes;
            Changed?.Invoke(this, EventArgs.Empty);
            try
            {
                await _store.SaveAsync(notes);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogNotSaved(_logger, ex);
                return false;
            }
        }

        // One line, no stray spaces or full stop, never longer than allowed.
        private static string Tidy(string text)
        {
            var line = Spaces().Replace(text, " ").Trim().TrimEnd('.', '!', ';', ',').TrimEnd();
            return line.Length > MaxNoteLength ? line[..MaxNoteLength].TrimEnd() : line;
        }

        private static readonly Regex RememberRequestPattern = new(@"^(?:(?:hey|hi|ok|okay|so|rider|please|pls|can you|could you|will you|would you|i want you to|i need you to|i'd like you to)[\s,!.]+)*(?:please\s+)?(?:remember|don'?t forget|do not forget|make a note|note down|keep in mind)(?:\s+that\b|\s*[:,-])?\s+(?<fact>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        private static Regex RememberRequest() => RememberRequestPattern;

        private static readonly Regex SpacesPattern = new(@"\s+", RegexOptions.Compiled);

        private static Regex Spaces() => SpacesPattern;

        private static readonly Regex ApostrophesPattern = new("['’]", RegexOptions.Compiled);

        private static Regex Apostrophes() => ApostrophesPattern;

        private static void LogNotSaved(ILogger logger, Exception exception)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.Log(LogLevel.Warning, "The rider's memory could not be saved on this computer.", exception);
            }
        }

        private static void LogNotLoaded(ILogger logger, Exception exception)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.Log(LogLevel.Warning, "The rider's saved memory could not be read. It starts empty.", exception);
            }
        }
    }
}
