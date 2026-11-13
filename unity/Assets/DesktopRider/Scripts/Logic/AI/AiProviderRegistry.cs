#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopRider.AI
{
    /// <summary>The AI providers this version of Desktop Rider can use, looked up by the name stored in settings.</summary>
    public sealed class AiProviderRegistry
    {
        private readonly Dictionary<string, IAiProvider> _providers;

        public AiProviderRegistry(IEnumerable<IAiProvider> providers)
        {
            Guard.NotNull(providers, nameof(providers));
            _providers = new Dictionary<string, IAiProvider>(StringComparer.OrdinalIgnoreCase);
            foreach (var provider in providers)
            {
                if (!_providers.TryAdd(provider.Name, provider))
                {
                    throw new ArgumentException($"Two AI providers are both called \"{provider.Name}\".", nameof(providers));
                }
            }
        }

        /// <summary>The names the user can choose from.</summary>
        public IReadOnlyCollection<string> Names => _providers.Keys;

        public IAiProvider Get(string? name)
        {
            if (name is not null && _providers.TryGetValue(name.Trim(), out var provider))
            {
                return provider;
            }

            throw new AiProviderException(
                AiErrorKind.UnknownProvider,
                $"Desktop Rider does not know the AI provider \"{name}\". Choose one of: {string.Join(", ", _providers.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))}.");
        }
    }
}
