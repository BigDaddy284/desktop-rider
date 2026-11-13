using System.Text.Json.Serialization;

namespace DesktopRider.AI.Ollama;

// The shapes Ollama's HTTP interface uses. Only the parts Desktop Rider needs.

internal sealed record OllamaTagsResponse(
    [property: JsonPropertyName("models")] IReadOnlyList<OllamaModel>? Models);

internal sealed record OllamaModel(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("size")] long? Size,
    [property: JsonPropertyName("details")] OllamaModelDetails? Details);

internal sealed record OllamaModelDetails(
    [property: JsonPropertyName("parameter_size")] string? ParameterSize,
    [property: JsonPropertyName("quantization_level")] string? QuantizationLevel);

internal sealed record OllamaChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OllamaChatMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("options"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OllamaChatOptions? Options);

internal sealed record OllamaChatOptions(
    [property: JsonPropertyName("temperature")] double Temperature);

internal sealed record OllamaChatMessage(
    [property: JsonPropertyName("role")] string? Role,
    [property: JsonPropertyName("content")] string? Content);

internal sealed record OllamaChatChunk(
    [property: JsonPropertyName("message")] OllamaChatMessage? Message,
    [property: JsonPropertyName("done")] bool Done,
    [property: JsonPropertyName("error")] string? Error);

internal sealed record OllamaErrorResponse(
    [property: JsonPropertyName("error")] string? Error);
