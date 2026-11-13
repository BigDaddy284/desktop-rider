using System.Runtime.CompilerServices;
using DesktopRider.Core.Configuration;

namespace DesktopRider.AI;

/// <summary>The result of checking the AI connection.</summary>
/// <param name="Models">The models found. Empty when the check failed.</param>
/// <param name="SelectedModel">The model chosen in settings, if any.</param>
/// <param name="Problem">What went wrong, or null when the provider answered.</param>
public sealed record AiConnectionReport(IReadOnlyList<AiModelInfo> Models, string? SelectedModel, AiProviderException? Problem)
{
    public bool Connected => Problem is null;

    /// <summary>True when a model is chosen and the provider still has it.</summary>
    public bool SelectedModelAvailable =>
        SelectedModel is not null && Models.Any(model => string.Equals(model.Name, SelectedModel, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// What the rest of Desktop Rider uses to talk to the AI. It applies the user's settings (provider, model,
/// personality, what the rider remembers, temperature) so callers only supply the conversation.
/// </summary>
public interface IAiChatService
{
    /// <summary>Tests the connection and lists the models. Never throws for a provider problem; the report carries it.</summary>
    Task<AiConnectionReport> CheckConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>Remembers the user's choice of model.</summary>
    Task SelectModelAsync(string model, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the conversation and returns the rider's reply piece by piece.
    /// Throws <see cref="AiProviderException"/> for problems and <see cref="OperationCanceledException"/> when stopped.
    /// </summary>
    IAsyncEnumerable<string> StreamReplyAsync(IReadOnlyList<ChatMessage> conversation, CancellationToken cancellationToken = default);
}

public sealed class AiChatService(AiProviderRegistry providers, ISettingsService settings, RiderMemory? memory = null) : IAiChatService, IDisposable
{
    private readonly AiProviderRegistry _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    private readonly ISettingsService _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    // Cancelled when the application closes, which stops any request still in progress.
    private readonly CancellationTokenSource _shutdown = new();
    private bool _disposed;

    public async Task<AiConnectionReport> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var ai = _settings.Current.Ai;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        try
        {
            var models = await _providers.Get(ai.Provider).ListModelsAsync(linked.Token).ConfigureAwait(false);
            return new AiConnectionReport(models, ai.Model, null);
        }
        catch (AiProviderException problem)
        {
            return new AiConnectionReport([], ai.Model, problem);
        }
    }

    public Task SelectModelAsync(string model, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        return _settings.UpdateAsync(s => s.Ai.Model = model, cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamReplyAsync(
        IReadOnlyList<ChatMessage> conversation,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var ai = _settings.Current.Ai;
        if (string.IsNullOrWhiteSpace(ai.Model))
        {
            throw new AiProviderException(
                AiErrorKind.NoModelSelected,
                "No AI model has been chosen yet. Check the AI connection, then choose a model.");
        }

        var provider = _providers.Get(ai.Provider);
        var personality = RiderPersonality.Compose(ai, memory?.ForConversation());
        List<ChatMessage> messages = [new(ChatRole.System, personality), .. conversation.Where(message => message.Role != ChatRole.System)];
        var request = new ChatRequest(ai.Model, messages, ai.Temperature);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        await foreach (var piece in provider.StreamChatAsync(request, linked.Token).ConfigureAwait(false))
        {
            yield return piece;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _shutdown.Dispose();
    }
}
