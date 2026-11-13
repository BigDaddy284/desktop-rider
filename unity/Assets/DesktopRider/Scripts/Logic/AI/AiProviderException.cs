#nullable enable

using System;

namespace DesktopRider.AI
{
    /// <summary>The kinds of problem an AI provider can run into.</summary>
    public enum AiErrorKind
    {
        /// <summary>The provider could not be reached at all, for example because Ollama is not running.</summary>
        NotRunning,

        /// <summary>The provider did not answer within the allowed time.</summary>
        Timeout,

        /// <summary>The user has not chosen a model yet.</summary>
        NoModelSelected,

        /// <summary>The chosen model is not installed any more.</summary>
        ModelNotFound,

        /// <summary>The connection dropped part-way through a reply.</summary>
        ConnectionLost,

        /// <summary>The provider answered with something that could not be understood.</summary>
        InvalidResponse,

        /// <summary>The provider reported a problem of its own.</summary>
        ProviderError,

        /// <summary>The settings name a provider this version does not know.</summary>
        UnknownProvider,
    }

    /// <summary>
    /// A problem talking to an AI provider. <see cref="Exception.Message"/> is always written in plain English
    /// and is safe to show to the user as it is.
    /// </summary>
    public sealed class AiProviderException : Exception
    {
        public AiProviderException(AiErrorKind kind, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            Kind = kind;
        }

        public AiErrorKind Kind { get; }
    }
}
