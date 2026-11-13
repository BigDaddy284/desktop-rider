using DesktopRider.AI.Ollama;
using DesktopRider.Core;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopRider.AI.Tests;

public class CompositionTests
{
    [Fact]
    public async Task AiServices_ResolveTogetherWithCore_AndAddTheirLinesToTheIconMenu()
    {
        var folder = Path.Combine(Path.GetTempPath(), "DesktopRider.AI.Tests." + Guid.NewGuid().ToString("N"));
        try
        {
            var services = new ServiceCollection().AddDesktopRiderCore(new AppPaths(folder)).AddDesktopRiderAi();
            services.AddSingleton<OverlayViewModel>();
            services.AddSingleton<TrayViewModel>();
            var provider = services.BuildServiceProvider(validateScopes: true);
            await using (provider.ConfigureAwait(true))
            {
                Assert.IsType<OllamaProvider>(provider.GetRequiredService<AiProviderRegistry>().Get(AiSettings.OllamaProvider));
                Assert.IsType<AiChatService>(provider.GetRequiredService<IAiChatService>());

                var menu = provider.GetRequiredService<TrayViewModel>().BuildMenu();

                Assert.Contains(menu, entry => entry.Text == AiMenuViewModel.CheckText);
                Assert.Contains(menu, entry => entry.Text == AiMenuViewModel.TalkText);

                // The conversation and its speech bubble share one conversation.
                var conversation = provider.GetRequiredService<ConversationViewModel>();
                Assert.Same(conversation, provider.GetRequiredService<ConversationViewModel>());
                Assert.False(provider.GetRequiredService<SpeechBubbleViewModel>().IsVisible);
                Assert.Equal(TrayViewModel.ExitText, menu[^1].Text);

                // Personality and memory: the settings, the saved conversation and what the rider remembers.
                Assert.Contains(menu, entry => entry.Text == AiMenuViewModel.SettingsText);
                Assert.IsType<JsonConversationStore>(provider.GetRequiredService<IConversationStore>());
                Assert.IsType<JsonMemoryStore>(provider.GetRequiredService<IMemoryStore>());
                Assert.Same(provider.GetRequiredService<RiderMemory>(), provider.GetRequiredService<RiderMemory>());
                Assert.True(provider.GetRequiredService<RiderSettingsViewModel>().KeepConversations);

                // The rider speaking first is offered on the menu, switched off, and sets no timer until switched on.
                var comments = provider.GetRequiredService<ProactiveComments>();
                Assert.Contains(menu, entry => entry.Text == ProactiveComments.EnableText && !entry.IsChecked);
                comments.Start();
                Assert.False(comments.Enabled);
                Assert.Null(comments.NextDue);

                // A conversation is kept in the data folder and is there again after a restart.
                await provider.GetRequiredService<RiderMemory>().RememberAsync("My bike is a 250");
                await provider.GetRequiredService<IConversationStore>().SaveAsync([new SavedLine(Speaker.User, "Hello", DateTimeOffset.UnixEpoch.AddYears(60))]);
                Assert.True(File.Exists(Path.Combine(folder, "memory.json")));
                Assert.True(File.Exists(Path.Combine(folder, "conversation.json")));
            }
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void Connection_ComesFromTheUsersSettings()
    {
        var ai = new AiSettings { OllamaAddress = "http://192.168.0.5:9999/", RequestTimeoutSeconds = 45 };

        var connection = ServiceCollectionExtensions.ConnectionFrom(ai);

        Assert.Equal("http://192.168.0.5:9999", connection.DisplayAddress);
        Assert.Equal(TimeSpan.FromSeconds(45), connection.ResponseTimeout);
        Assert.Equal(OllamaConnection.DefaultDiscoveryTimeout, connection.DiscoveryTimeout);
    }

    [Fact]
    public void Connection_WithUnusableSettings_FallsBackToTheStandardLocalAddress()
    {
        var ai = new AiSettings { OllamaAddress = "not an address", RequestTimeoutSeconds = 0 };

        var connection = ServiceCollectionExtensions.ConnectionFrom(ai);

        Assert.Equal(AiSettings.DefaultOllamaAddress, connection.DisplayAddress);
        Assert.Equal(TimeSpan.FromSeconds(AiSettings.MinTimeoutSeconds), connection.ResponseTimeout);
    }
}
