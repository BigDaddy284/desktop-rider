using DesktopRider.Core.ViewModels;

namespace DesktopRider.AI.Tests;

public class AiMenuViewModelTests
{
    [Fact]
    public void Menu_AtFirst_OffersTalkingAndTheConnectionCheck_AndContactsNothing()
    {
        using var context = new Context(model: null);

        var menu = context.Menu.BuildEntries();

        Assert.Equal([AiMenuViewModel.TalkText, AiMenuViewModel.CheckText], menu.Select(entry => entry.Text));
        Assert.Equal(0, context.Provider.ListCalls);
        Assert.Empty(context.Provider.Requests);
    }

    [Fact]
    public async Task Check_WithModelsAndNoneChosen_ListsThemToChooseFrom()
    {
        using var context = new Context(model: null);
        context.Provider.Models.AddRange([new("alpha", Details: "7B, Q4"), new("beta")]);

        await context.Menu.CheckConnectionAsync();

        var hint = Assert.Single(context.Hints);
        Assert.Equal("Ollama is connected", hint.Title);
        Assert.Contains("Found 2 AI models", hint.Message, StringComparison.Ordinal);
        Assert.Contains(AiMenuViewModel.ChooseModelText, hint.Message, StringComparison.Ordinal);
        var menu = context.Menu.BuildEntries();
        Assert.Equal([AiMenuViewModel.TalkText, AiMenuViewModel.CheckText, AiMenuViewModel.ChooseModelText], menu.Select(entry => entry.Text));
        Assert.False(menu[2].IsSeparator);
        Assert.Equal(["alpha (7B, Q4)", "beta"], menu[2].Children!.Select(entry => entry.Text));
        Assert.DoesNotContain(menu[2].Children!, entry => entry.IsChecked);
    }

    [Fact]
    public async Task ChoosingAModel_SavesIt_TicksIt_AndSaysHowToStartTalking()
    {
        using var context = new Context(model: null);
        context.Provider.Models.AddRange([new("alpha"), new("beta")]);
        await context.Menu.CheckConnectionAsync();
        context.Hints.Clear();

        context.Menu.BuildEntries()[2].Children![1].Command!.Execute(null);

        Assert.Equal("beta", context.Settings.Current.Ai.Model);
        var hint = Assert.Single(context.Hints).Message;
        Assert.Contains("beta", hint, StringComparison.Ordinal);
        Assert.Contains(AiMenuViewModel.TalkText, hint, StringComparison.Ordinal);
        var menu = context.Menu.BuildEntries();
        Assert.Equal([AiMenuViewModel.TalkText, AiMenuViewModel.CheckText, AiMenuViewModel.ChooseModelText], menu.Select(entry => entry.Text));
        Assert.Equal([false, true], menu[2].Children!.Select(entry => entry.IsChecked));
    }

    [Fact]
    public async Task ChoosingAModel_WhenItCannotBeSaved_SaysSo()
    {
        using var context = new Context(model: null);
        context.Settings.FailSaves = true;

        await context.Menu.SelectModelAsync("alpha");

        Assert.Equal("The choice could not be saved", Assert.Single(context.Hints).Title);
        Assert.Null(context.Settings.Current.Ai.Model);
    }

    [Fact]
    public async Task Check_WithTheChosenModelPresent_ConfirmsIt()
    {
        using var context = new Context(model: "alpha");
        context.Provider.Models.Add(new("alpha"));

        await context.Menu.CheckConnectionAsync();

        Assert.Contains("Found 1 AI model. The rider is using alpha.", context.Hints[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_WhenTheChosenModelHasGone_AsksForAnother()
    {
        using var context = new Context(model: "gone");
        context.Provider.Models.Add(new("alpha"));

        await context.Menu.CheckConnectionAsync();

        Assert.Contains("gone is no longer one of them", context.Hints[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_WithNoModelsInstalled_ExplainsWhatToDo()
    {
        using var context = new Context(model: null);

        await context.Menu.CheckConnectionAsync();

        Assert.Contains("no AI models yet", context.Hints[0].Message, StringComparison.Ordinal);
        Assert.Equal([AiMenuViewModel.TalkText, AiMenuViewModel.CheckText], context.Menu.BuildEntries().Select(entry => entry.Text));
    }

    [Fact]
    public async Task Check_WhenOllamaIsNotRunning_ShowsTheReasonAndRecoversNextTime()
    {
        using var context = new Context(model: null);
        context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");

        await context.Menu.CheckConnectionAsync();

        Assert.Equal("The AI is not connected", context.Hints[0].Title);
        Assert.Equal("Desktop Rider cannot reach Ollama.", context.Hints[0].Message);
        Assert.Equal([AiMenuViewModel.TalkText, AiMenuViewModel.CheckText], context.Menu.BuildEntries().Select(entry => entry.Text));

        context.Provider.Problem = null;
        context.Provider.Models.Add(new("alpha"));
        await context.Menu.CheckConnectionAsync();

        Assert.Equal("Ollama is connected", context.Hints[1].Title);
    }

    [Fact]
    public async Task UsingTheMenuAfterClosing_DoesNothingAndDoesNotFail()
    {
        using var context = new Context();
        context.Service.Dispose();

        await context.Menu.CheckConnectionAsync();

        Assert.Empty(context.Hints);
    }

    [Fact]
    public void TalkToTheRider_AsksForTheConversation_WithoutContactingTheAi()
    {
        using var context = new Context(model: null);
        var asked = 0;
        context.Menu.TalkRequested += (_, _) => asked++;

        context.Menu.BuildEntries().Single(entry => entry.Text == AiMenuViewModel.TalkText).Command!.Execute(null);

        Assert.Equal(1, asked);
        Assert.Equal(0, context.Provider.ListCalls);
        Assert.Empty(context.Provider.Requests);
    }

    [Fact]
    public async Task Check_SaysItHasStartedBeforeTheResultArrives()
    {
        using var context = new Context(model: null);
        var order = new List<string>();
        context.Menu.ProgressChanged += (_, progress) => order.Add("progress: " + progress.Title);
        context.Menu.MessageRequested += (_, message) => order.Add("result: " + message.Title);

        await context.Menu.CheckConnectionAsync();

        Assert.Equal(["progress: Checking the AI connection", "result: Ollama is connected"], order);
    }

    [Fact]
    public async Task TheMenuCommand_RunsTheCheck()
    {
        using var context = new Context(model: null);
        context.Provider.Models.Add(new("alpha"));

        context.Menu.BuildEntries().Single(entry => entry.Text == AiMenuViewModel.CheckText).Command!.Execute(null);
        await context.HintArrived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, context.Provider.ListCalls);
    }

    private sealed class Context : IDisposable
    {
        public Context(string? model = "picked-model")
        {
            Settings.Current.Ai.Model = model;
            Service = new AiChatService(new AiProviderRegistry([Provider]), Settings);
            Menu = new AiMenuViewModel(Service, Settings);
            Menu.ProgressChanged += (_, progress) => Progress.Add(progress);
            Menu.MessageRequested += (_, hint) =>
            {
                Hints.Add(hint);
                HintArrived.TrySetResult();
            };
        }

        public FakeSettingsService Settings { get; } = new();

        public FakeProvider Provider { get; } = new();

        public AiChatService Service { get; }

        public AiMenuViewModel Menu { get; }

        public List<AiMessageEventArgs> Hints { get; } = [];

        public List<AiMessageEventArgs> Progress { get; } = [];

        public TaskCompletionSource HintArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Dispose()
        {
            Service.Dispose();
        }
    }
}
