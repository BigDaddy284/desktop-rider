#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json;

namespace DesktopRider.AI.Ollama
{
    // The shapes Ollama's HTTP interface uses. Only the parts Desktop Rider needs.

    internal sealed record OllamaTagsResponse(
        [property: JsonProperty("models")] IReadOnlyList<OllamaModel>? Models);

    internal sealed record OllamaModel(
        [property: JsonProperty("name")] string? Name,
        [property: JsonProperty("size")] long? Size,
        [property: JsonProperty("details")] OllamaModelDetails? Details);

    internal sealed record OllamaModelDetails(
        [property: JsonProperty("parameter_size")] string? ParameterSize,
        [property: JsonProperty("quantization_level")] string? QuantizationLevel);

    internal sealed record OllamaChatRequest(
        [property: JsonProperty("model")] string Model,
        [property: JsonProperty("messages")] IReadOnlyList<OllamaChatMessage> Messages,
        [property: JsonProperty("stream")] bool Stream,
        [property: JsonProperty("options", NullValueHandling = NullValueHandling.Ignore)] OllamaChatOptions? Options);

    internal sealed record OllamaChatOptions(
        [property: JsonProperty("temperature")] double Temperature);

    internal sealed record OllamaChatMessage(
        [property: JsonProperty("role")] string? Role,
        [property: JsonProperty("content")] string? Content);

    internal sealed record OllamaChatChunk(
        [property: JsonProperty("message")] OllamaChatMessage? Message,
        [property: JsonProperty("done")] bool Done,
        [property: JsonProperty("error")] string? Error);

    internal sealed record OllamaErrorResponse(
        [property: JsonProperty("error")] string? Error);
}
