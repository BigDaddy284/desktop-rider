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
                Assert.Equal(TrayViewModel.ExitText, menu[^1].Text);
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
