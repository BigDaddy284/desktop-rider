#nullable enable

using System;
using System.Net.Http;
using System.Threading;
using DesktopRider.AI.Ollama;
using DesktopRider.Core;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.ViewModels;

namespace DesktopRider.AI
{
    /// <summary>
    /// Everything the rider needs to talk, remember and act, joined together. Each part is made once, the first
    /// time it is asked for. Making these parts makes no request; Ollama is only contacted when the user asks for
    /// something, or, if the user has switched it on, when the rider starts a conversation itself.
    /// </summary>
    /// <remarks>
    /// Ask for <see cref="ProactiveComments"/> and <see cref="ProactiveAnimations"/> on the interface thread
    /// (Unity's main thread), because that is where the rider's own remarks and movements must be made.
    /// </remarks>
    public sealed class RiderServices : ServiceSet
    {
        private readonly Random? _random;
        private IAiProvider? _provider;
        private AiProviderRegistry? _providers;
        private IMemoryStore? _memoryStore;
        private RiderMemory? _memory;
        private IAiChatService? _chat;
        private AiMenuViewModel? _aiMenu;
        private IConversationStore? _conversationStore;
        private ConversationViewModel? _conversation;
        private SpeechBubbleViewModel? _speechBubble;
        private RiderSettingsViewModel? _riderSettings;
        private ProactiveComments? _proactiveComments;
        private ProactiveAnimations? _proactiveAnimations;
        private OverlayViewModel? _overlay;
        private TrayViewModel? _tray;

        /// <param name="core">File locations, the clock, logging and settings. Not closed by this object.</param>
        /// <param name="random">Only supplied by tests, to make the rider's choices predictable.</param>
        public RiderServices(CoreServices core, Random? random = null)
        {
            Core = core ?? throw new ArgumentNullException(nameof(core));
            _random = random;
        }

        public CoreServices Core { get; }

        public IAiProvider Provider => Once(ref _provider, () =>
        {
            var settings = Core.Settings;

            // Waiting is limited per request by the provider, because a streamed reply may rightly take a long time overall.
            var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            return new OllamaProvider(http, () => ConnectionFrom(settings.Current.Ai), Core.Loggers.CreateLogger<OllamaProvider>());
        });

        public AiProviderRegistry Providers => Once(ref _providers, () => new AiProviderRegistry(new[] { Provider }));

        public IMemoryStore MemoryStore => Once(ref _memoryStore, () =>
            new JsonMemoryStore(Core.Paths, Core.Loggers.CreateLogger<JsonMemoryStore>(), Core.Time));

        public RiderMemory Memory => Once(ref _memory, () =>
            new RiderMemory(MemoryStore, Core.Settings, Core.Time, Core.Loggers.CreateLogger<RiderMemory>()));

        public IAiChatService Chat => Once(ref _chat, () => new AiChatService(Providers, Core.Settings, Memory));

        public AiMenuViewModel AiMenu => Once(ref _aiMenu, () =>
            new AiMenuViewModel(Chat, Core.Settings, Core.Loggers.CreateLogger<AiMenuViewModel>()));

        public IConversationStore ConversationStore => Once(ref _conversationStore, () =>
            new JsonConversationStore(Core.Paths, Core.Loggers.CreateLogger<JsonConversationStore>(), Core.Time));

        public ConversationViewModel Conversation => Once(ref _conversation, () => new ConversationViewModel(
            Chat,
            Core.Settings,
            Core.Loggers.CreateLogger<ConversationViewModel>(),
            ConversationStore,
            Core.Time,
            Memory,
            _random));

        public SpeechBubbleViewModel SpeechBubble => Once(ref _speechBubble, () => new SpeechBubbleViewModel(Conversation));

        public RiderSettingsViewModel RiderSettings => Once(ref _riderSettings, () =>
            new RiderSettingsViewModel(Core.Settings, Conversation, Memory, Core.Loggers.CreateLogger<RiderSettingsViewModel>()));

        public ProactiveComments ProactiveComments => Once(ref _proactiveComments, () => new ProactiveComments(
            Conversation,
            Core.Settings,
            Core.Time,
            Core.Loggers.CreateLogger<ProactiveComments>(),
            OnThisThread()));

        public ProactiveAnimations ProactiveAnimations => Once(ref _proactiveAnimations, () => new ProactiveAnimations(
            ProactiveComments,
            Core.Settings,
            Core.Time,
            _random,
            OnThisThread()));

        public OverlayViewModel Overlay => Once(ref _overlay, () =>
            new OverlayViewModel(Core.Settings, Core.Loggers.CreateLogger<OverlayViewModel>()));

        /// <summary>The notification-area menu, with the AI lines and the "speak first" line added, in that order.</summary>
        public TrayViewModel Tray => Once(ref _tray, () => new TrayViewModel(
            Overlay,
            Core.Settings,
            new ITrayMenuContributor[] { AiMenu, ProactiveComments },
            Core.Loggers.CreateLogger<TrayViewModel>()));

        internal static OllamaConnection ConnectionFrom(AiSettings ai) => new(
            new Uri(SettingsNormalizer.NormalizeAddress(ai.OllamaAddress)),
            TimeSpan.FromSeconds(Math.Clamp(ai.RequestTimeoutSeconds, AiSettings.MinTimeoutSeconds, AiSettings.MaxTimeoutSeconds)),
            OllamaConnection.DefaultDiscoveryTimeout);

        // Work that must happen on the thread asking now (the interface thread at start-up) is posted back to it later.
        private static Action<Action>? OnThisThread()
        {
            var interfaceThread = SynchronizationContext.Current;
            return interfaceThread is null ? (Action<Action>?)null : work => interfaceThread.Post(_ => work(), null);
        }
    }
}
