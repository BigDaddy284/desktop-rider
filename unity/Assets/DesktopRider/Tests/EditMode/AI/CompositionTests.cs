#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using DesktopRider.AI.Ollama;
using DesktopRider.Core;
using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;
using DesktopRider.Core.ViewModels;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    public class CompositionTests
    {
        [Test]
        public async Task AiServices_ResolveTogetherWithCore_AndAddTheirLinesToTheIconMenu()
        {
            var folder = Path.Combine(Path.GetTempPath(), "DesktopRider.AI.Tests." + Guid.NewGuid().ToString("N"));
            try
            {
                using var core = new CoreServices(new AppPaths(folder));
                using (var services = new RiderServices(core))
                {
                    Assert.IsType<OllamaProvider>(services.Providers.Get(AiSettings.OllamaProvider));
                    Assert.IsType<AiChatService>(services.Chat);

                    var menu = services.Tray.BuildMenu();

                    Assert.Contains(menu, entry => entry.Text == AiMenuViewModel.CheckText);
                    Assert.Contains(menu, entry => entry.Text == AiMenuViewModel.TalkText);

                    // The conversation and its speech bubble share one conversation.
                    var conversation = services.Conversation;
                    Assert.Same(conversation, services.Conversation);
                    Assert.False(services.SpeechBubble.IsVisible);
                    Assert.Equal(TrayViewModel.ExitText, menu[^1].Text);

                    // Personality and memory: the settings, the saved conversation and what the rider remembers.
                    Assert.Contains(menu, entry => entry.Text == AiMenuViewModel.SettingsText);
                    Assert.IsType<JsonConversationStore>(services.ConversationStore);
                    Assert.IsType<JsonMemoryStore>(services.MemoryStore);
                    Assert.Same(services.Memory, services.Memory);
                    Assert.True(services.RiderSettings.KeepConversations);

                    // The rider speaking first is offered on the menu, switched off, and sets no timer until switched on.
                    var comments = services.ProactiveComments;
                    Assert.Contains(menu, entry => entry.Text == ProactiveComments.EnableText && !entry.IsChecked);
                    comments.Start();
                    Assert.False(comments.Enabled);
                    Assert.Null(comments.NextDue);

                    // Nor does the rider move by itself until then.
                    var animations = services.ProactiveAnimations;
                    animations.Start();
                    Assert.False(animations.Active);
                    Assert.Null(animations.NextDue);

                    // A conversation is kept in the data folder and is there again after a restart.
                    await services.Memory.RememberAsync("My bike is a 250");
                    await services.ConversationStore.SaveAsync(new[] { new SavedLine(Speaker.User, "Hello", DateTimeOffset.UnixEpoch.AddYears(60)) });
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

        [Test]
        public void Connection_ComesFromTheUsersSettings()
        {
            var ai = new AiSettings { OllamaAddress = "http://192.168.0.5:9999/", RequestTimeoutSeconds = 45 };

            var connection = RiderServices.ConnectionFrom(ai);

            Assert.Equal("http://192.168.0.5:9999", connection.DisplayAddress);
            Assert.Equal(TimeSpan.FromSeconds(45), connection.ResponseTimeout);
            Assert.Equal(OllamaConnection.DefaultDiscoveryTimeout, connection.DiscoveryTimeout);
        }

        [Test]
        public void Connection_WithUnusableSettings_FallsBackToTheStandardLocalAddress()
        {
            var ai = new AiSettings { OllamaAddress = "not an address", RequestTimeoutSeconds = 0 };

            var connection = RiderServices.ConnectionFrom(ai);

            Assert.Equal(AiSettings.DefaultOllamaAddress, connection.DisplayAddress);
            Assert.Equal(TimeSpan.FromSeconds(AiSettings.MinTimeoutSeconds), connection.ResponseTimeout);
        }
    }
}
