#nullable enable

using System;

namespace DesktopRider.AI.Ollama
{
    /// <summary>Where Ollama is and how long to wait for it. Read fresh for every request so a settings change applies at once.</summary>
    /// <param name="Address">The base address, for example <c>http://localhost:11434</c>.</param>
    /// <param name="ResponseTimeout">The longest silence allowed while waiting for a reply or for its next piece.</param>
    /// <param name="DiscoveryTimeout">The longest wait when checking the connection and listing models.</param>
    public sealed record OllamaConnection(Uri Address, TimeSpan ResponseTimeout, TimeSpan DiscoveryTimeout)
    {
        /// <summary>Listing models is quick when Ollama is there, so a short wait keeps the connection test snappy.</summary>
        public static readonly TimeSpan DefaultDiscoveryTimeout = TimeSpan.FromSeconds(10);

        /// <summary>The address as shown to the user and written to the log, without any sign-in details it may contain.</summary>
        public string DisplayAddress => $"{Address.Scheme}://{Address.Host}:{Address.Port}";

        internal Uri Endpoint(string path) => new(new Uri(Address.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/"), path);
    }
}
