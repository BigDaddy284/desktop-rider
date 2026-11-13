using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DesktopRider.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DesktopRider.AI.Ollama;

/// <summary>
/// Talks to a locally running Ollama. Requests are only ever made when asked for; nothing runs in the background.
/// Conversation text is never written to the log.
/// </summary>
public sealed partial class OllamaProvider : IAiProvider, IDisposable
{
    private const int MaxErrorTextLength = 200;

    private readonly HttpClient _http;
    private readonly Func<OllamaConnection> _connection;
    private readonly ILogger<OllamaProvider> _logger;

    /// <param name="http">Used for every request and disposed with the provider. Its own timeout must be switched off; waiting is limited per request instead.</param>
    /// <param name="connection">Supplies the current address and waiting times.</param>
    public OllamaProvider(HttpClient http, Func<OllamaConnection> connection, ILogger<OllamaProvider>? logger = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _logger = logger ?? NullLogger<OllamaProvider>.Instance;
    }

    public string Name => AiSettings.OllamaProvider;

    public async Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var connection = _connection();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(connection.DiscoveryTimeout);
        try
        {
            using var response = await _http
                .GetAsync(connection.Endpoint("api/tags"), HttpCompletionOption.ResponseHeadersRead, limit.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw await ErrorFromResponseAsync(response, model: null, limit.Token).ConfigureAwait(false);
            }

            var tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(limit.Token).ConfigureAwait(false)
                ?? throw new JsonException("The model list was empty.");
            var models = (tags.Models ?? [])
                .Where(model => !string.IsNullOrWhiteSpace(model.Name))
                .Select(model => new AiModelInfo(model.Name!, model.Size, Describe(model.Details)))
                .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            LogModelsFound(_logger, models.Count, connection.DisplayAddress);
            return models;
        }
        catch (Exception ex) when (Translate(ex, connection, midReply: false, cancellationToken) is { } problem)
        {
            LogProblem(_logger, problem.Kind, connection.DisplayAddress, ex);
            throw problem;
        }
    }

    public async IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Model);

        var connection = _connection();

        // The limit is on silence, not on the whole reply: it starts again each time Ollama sends something.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(connection.ResponseTimeout);

        HttpResponseMessage response;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, connection.Endpoint("api/chat"))
            {
                Content = JsonContent.Create(ToWire(request)),
            };
            response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                using (response)
                {
                    throw await ErrorFromResponseAsync(response, request.Model, limit.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (Translate(ex, connection, midReply: false, cancellationToken) is { } problem)
        {
            LogProblem(_logger, problem.Kind, connection.DisplayAddress, ex);
            throw problem;
        }

        using (response)
        {
            StreamReader reader;
            try
            {
                var stream = await response.Content.ReadAsStreamAsync(limit.Token).ConfigureAwait(false);
                reader = new StreamReader(stream, Encoding.UTF8);
            }
            catch (Exception ex) when (Translate(ex, connection, midReply: true, cancellationToken) is { } problem)
            {
                LogProblem(_logger, problem.Kind, connection.DisplayAddress, ex);
                throw problem;
            }

            using (reader)
            {
                var finished = false;
                while (!finished)
                {
                    string? piece;
                    try
                    {
                        var line = await reader.ReadLineAsync(limit.Token).ConfigureAwait(false)
                            ?? throw new AiProviderException(AiErrorKind.ConnectionLost, ConnectionLostMessage);
                        limit.CancelAfter(connection.ResponseTimeout);
                        if (line.Length == 0)
                        {
                            continue;
                        }

                        var chunk = JsonSerializer.Deserialize<OllamaChatChunk>(line)
                            ?? throw new JsonException("A piece of the reply was empty.");
                        if (chunk.Error is not null)
                        {
                            throw ErrorFromText(chunk.Error, request.Model, HttpStatusCode.OK);
                        }

                        piece = chunk.Message?.Content;
                        finished = chunk.Done;
                    }
                    catch (Exception ex) when (Translate(ex, connection, midReply: true, cancellationToken) is { } problem)
                    {
                        LogProblem(_logger, problem.Kind, connection.DisplayAddress, ex);
                        throw problem;
                    }

                    if (!string.IsNullOrEmpty(piece))
                    {
                        yield return piece;
                    }
                }
            }
        }
    }

    public void Dispose() => _http.Dispose();

    private const string ConnectionLostMessage =
        "The connection to Ollama was lost part-way through the reply. Check that Ollama is still running, then try again.";

    private static OllamaChatRequest ToWire(ChatRequest request) => new(
        request.Model,
        [.. request.Messages.Select(message => new OllamaChatMessage(RoleName(message.Role), message.Content))],
        Stream: true,
        request.Temperature is { } temperature ? new OllamaChatOptions(temperature) : null);

    private static string RoleName(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown conversation role."),
    };

    private static string? Describe(OllamaModelDetails? details)
    {
        string?[] parts = [details?.ParameterSize, details?.QuantizationLevel];
        var text = string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    /// Turns a low-level failure into a plain-English problem. Returns null for things that must pass through untouched:
    /// problems already explained, and cancellation asked for by the caller.
    /// </summary>
    private static AiProviderException? Translate(Exception exception, OllamaConnection connection, bool midReply, CancellationToken callerToken) => exception switch
    {
        AiProviderException => null,
        OperationCanceledException when callerToken.IsCancellationRequested => null,
        OperationCanceledException => new AiProviderException(
            AiErrorKind.Timeout,
            midReply
                ? "The AI model stopped answering part-way through. Try again in a moment."
                : $"Ollama at {connection.DisplayAddress} did not answer in time. If a large model is still loading, try again in a moment.",
            exception),
        HttpRequestException or IOException when midReply => new AiProviderException(AiErrorKind.ConnectionLost, ConnectionLostMessage, exception),
        HttpRequestException or IOException => new AiProviderException(
            AiErrorKind.NotRunning,
            $"Desktop Rider cannot reach Ollama at {connection.DisplayAddress}. Check that Ollama is running on this computer, then try again.",
            exception),
        JsonException or NotSupportedException => new AiProviderException(
            AiErrorKind.InvalidResponse,
            $"The answer from {connection.DisplayAddress} was not what Ollama normally sends. Check that the address in settings really is Ollama.",
            exception),
        _ => null,
    };

    private static async Task<AiProviderException> ErrorFromResponseAsync(HttpResponseMessage response, string? model, CancellationToken cancellationToken)
    {
        string? text = null;
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            text = JsonSerializer.Deserialize<OllamaErrorResponse>(body)?.Error;
        }
        catch (JsonException)
        {
            // Not Ollama's usual error shape; the status code alone is reported.
        }

        return ErrorFromText(text, model, response.StatusCode);
    }

    private static AiProviderException ErrorFromText(string? text, string? model, HttpStatusCode status)
    {
        var missingModel = model is not null
            && (status == HttpStatusCode.NotFound || (text?.Contains("not found", StringComparison.OrdinalIgnoreCase) ?? false));
        if (missingModel)
        {
            return new AiProviderException(
                AiErrorKind.ModelNotFound,
                $"Ollama does not have the AI model \"{model}\". It may have been removed. Choose another model.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new AiProviderException(AiErrorKind.ProviderError, $"Ollama reported a problem (code {(int)status}). Try again in a moment.");
        }

        var trimmed = text.Trim();
        if (trimmed.Length > MaxErrorTextLength)
        {
            trimmed = trimmed[..MaxErrorTextLength] + "…";
        }

        return new AiProviderException(AiErrorKind.ProviderError, $"Ollama reported a problem: {trimmed}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ollama at {Address} has {Count} model(s).")]
    private static partial void LogModelsFound(ILogger logger, int count, string address);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Problem talking to Ollama at {Address}: {Kind}.")]
    private static partial void LogProblem(ILogger logger, AiErrorKind kind, string address, Exception exception);
}
