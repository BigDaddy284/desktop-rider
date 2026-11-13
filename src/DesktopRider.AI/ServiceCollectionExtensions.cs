using DesktopRider.AI.Ollama;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DesktopRider.AI;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AI services. Requires <c>AddDesktopRiderCore</c>. Creating these services makes no request;
    /// Ollama is only contacted when the user asks for something.
    /// </summary>
    public static IServiceCollection AddDesktopRiderAi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAiProvider>(provider =>
        {
            var settings = provider.GetRequiredService<ISettingsService>();

            // Waiting is limited per request by the provider, because a streamed reply may rightly take a long time overall.
            var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            return new OllamaProvider(http, () => ConnectionFrom(settings.Current.Ai), provider.GetRequiredService<ILogger<OllamaProvider>>());
        });
        services.AddSingleton<AiProviderRegistry>();
        services.AddSingleton<IMemoryStore>(provider => new JsonMemoryStore(
            provider.GetRequiredService<AppPaths>(),
            provider.GetRequiredService<ILogger<JsonMemoryStore>>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<RiderMemory>();
        services.AddSingleton<IAiChatService, AiChatService>();
        services.AddSingleton<AiMenuViewModel>();
        services.AddSingleton<IConversationStore>(provider => new JsonConversationStore(
            provider.GetRequiredService<AppPaths>(),
            provider.GetRequiredService<ILogger<JsonConversationStore>>(),
            provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider => new ConversationViewModel(
            provider.GetRequiredService<IAiChatService>(),
            provider.GetRequiredService<ISettingsService>(),
            provider.GetRequiredService<ILogger<ConversationViewModel>>(),
            provider.GetRequiredService<IConversationStore>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<RiderMemory>()));
        services.AddSingleton<SpeechBubbleViewModel>();
        services.AddSingleton<RiderSettingsViewModel>();
        services.AddSingleton<ITrayMenuContributor>(provider => provider.GetRequiredService<AiMenuViewModel>());
        return services;
    }

    internal static OllamaConnection ConnectionFrom(AiSettings ai) => new(
        new Uri(SettingsNormalizer.NormalizeAddress(ai.OllamaAddress)),
        TimeSpan.FromSeconds(Math.Clamp(ai.RequestTimeoutSeconds, AiSettings.MinTimeoutSeconds, AiSettings.MaxTimeoutSeconds)),
        OllamaConnection.DefaultDiscoveryTimeout);
}
