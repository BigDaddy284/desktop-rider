using DesktopRider.Core.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.AI;

/// <summary>One line of the conversation as it is kept on this computer.</summary>
/// <param name="From">Who said it. Explanations from Desktop Rider itself are never kept.</param>
/// <param name="Text">What was said.</param>
/// <param name="At">When it was said.</param>
public sealed record SavedLine(Speaker From, string Text, DateTimeOffset At);

/// <summary>Keeps the conversation between sessions. Everything stays on this computer.</summary>
public interface IConversationStore
{
    /// <summary>The saved conversation, oldest first. Never throws for a missing or damaged file; it is simply empty.</summary>
    Task<IReadOnlyList<SavedLine>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces the saved conversation.</summary>
    Task SaveAsync(IReadOnlyList<SavedLine> lines, CancellationToken cancellationToken = default);

    /// <summary>Removes the saved conversation from this computer.</summary>
    Task DeleteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Stores the conversation as a readable JSON file in the user's local data folder.</summary>
public sealed partial class JsonConversationStore : IConversationStore, IDisposable
{
    private readonly LocalJsonFile<SavedConversation> _file;
    private readonly ILogger<JsonConversationStore> _logger;

    public JsonConversationStore(AppPaths paths, ILogger<JsonConversationStore>? logger = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = new LocalJsonFile<SavedConversation>(paths.ConversationFile, timeProvider);
        _logger = logger ?? NullLogger<JsonConversationStore>.Instance;
    }

    public async Task<IReadOnlyList<SavedLine>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var read = await _file.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (read.Status == LocalFileStatus.Damaged)
        {
            LogDamagedFile(_logger, read.Problem, read.BackupFile);
        }

        // A file edited by hand may hold lines with nothing in them, or explanations that are never kept.
        return (read.Value?.Lines ?? [])
            .Where(line => line is { Text.Length: > 0, From: Speaker.User or Speaker.Rider })
            .ToList();
    }

    public Task SaveAsync(IReadOnlyList<SavedLine> lines, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return _file.WriteAsync(new SavedConversation { Lines = [.. lines] }, cancellationToken);
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default) => _file.DeleteAsync(cancellationToken);

    public void Dispose() => _file.Dispose();

    // Only the fact is logged. The words of the conversation never go in the log.
    [LoggerMessage(Level = LogLevel.Warning, Message = "The saved conversation could not be read, so the rider starts afresh. Backup of the damaged file: {BackupFile}")]
    private static partial void LogDamagedFile(ILogger logger, Exception? exception, string? backupFile);

    /// <summary>The shape of the file.</summary>
    internal sealed class SavedConversation
    {
        public int Version { get; set; } = 1;

        public List<SavedLine> Lines { get; set; } = [];
    }
}
