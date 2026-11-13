#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopRider.AI
{
    /// <summary>
    /// A source of AI replies, such as Ollama. Everything is asynchronous and can be cancelled.
    /// Problems are reported as <see cref="AiProviderException"/>; cancellation by the caller as
    /// <see cref="OperationCanceledException"/>. A provider never touches windows or animation.
    /// </summary>
    public interface IAiProvider
    {
        /// <summary>The name stored in settings to select this provider.</summary>
        string Name { get; }

        /// <summary>Asks the provider which models it has. Also serves as the connection test.</summary>
        Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken cancellationToken = default);

        /// <summary>Sends a conversation and returns the reply piece by piece as it is produced.</summary>
        IAsyncEnumerable<string> StreamChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
    }
}
