using DesktopRider.Core.ViewModels;

namespace DesktopRider.AI.Tests;

public class AiMenuViewModelTests
{
    [Fact]
    public void Menu_AtFirst_OnlyOffersTheConnectionCheck_AndContactsNothing()
    {
        using var context = new Context(model: null);

        var menu = context.Menu.BuildEntries();

        Assert.Equal([AiMenuViewModel.CheckText], menu.Select(entry => entry.Text));
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
        Assert.Equal([AiMenuViewModel.CheckText, AiMenuViewModel.ChooseModelText], menu.Select(entry => entry.Text));
        Assert.False(menu[1].IsSeparator);
        Assert.Equal(["alpha (7B, Q4)", "beta"], menu[1].Children!.Select(entry => entry.Text));
        Assert.DoesNotContain(menu[1].Children!, entry => entry.IsChecked);
    }

    [Fact]
    public async Task ChoosingAModel_SavesIt_TicksIt_AndOffersTheHello()
    {
        using var context = new Context(model: null);
        context.Provider.Models.AddRange([new("alpha"), new("beta")]);
        await context.Menu.CheckConnectionAsync();
        context.Hints.Clear();

        context.Menu.BuildEntries()[1].Children![1].Command!.Execute(null);

        Assert.Equal("beta", context.Settings.Current.Ai.Model);
        Assert.Contains("beta", Assert.Single(context.Hints).Message, StringComparison.Ordinal);
        var menu = context.Menu.BuildEntries();
        Assert.Equal([AiMenuViewModel.CheckText, AiMenuViewModel.ChooseModelText, AiMenuViewModel.SayHelloText], menu.Select(entry => entry.Text));
        Assert.Equal([false, true], menu[1].Children!.Select(entry => entry.IsChecked));
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
        Assert.Equal([AiMenuViewModel.CheckText], context.Menu.BuildEntries().Select(entry => entry.Text));
    }

    [Fact]
    public async Task Check_WhenOllamaIsNotRunning_ShowsTheReasonAndRecoversNextTime()
    {
        using var context = new Context(model: null);
        context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");

        await context.Menu.CheckConnectionAsync();

        Assert.Equal("The AI is not connected", context.Hints[0].Title);
        Assert.Equal("Desktop Rider cannot reach Ollama.", context.Hints[0].Message);
        Assert.Equal([AiMenuViewModel.CheckText], context.Menu.BuildEntries().Select(entry => entry.Text));

        context.Provider.Problem = null;
        context.Provider.Models.Add(new("alpha"));
        await context.Menu.CheckConnectionAsync();

        Assert.Equal("Ollama is connected", context.Hints[1].Title);
    }

    [Fact]
    public async Task SayHello_ShowsTheWholeReply()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["Braaap! ", "Hello ", "there."]);

        await context.Menu.SayHelloAsync();

        var hint = Assert.Single(context.Hints);
        Assert.Equal("The rider says", hint.Title);
        Assert.Equal("Braaap! Hello there.", hint.Message);
        Assert.Equal(AiMenuViewModel.HelloPrompt, context.Provider.Requests[0].Messages[^1].Content);
    }

    [Fact]
    public async Task SayHello_WithAVeryLongReply_ShortensItToFitTheMessage()
    {
        using var context = new Context();
        context.Provider.Reply.Add(new string('a', 1000));

        await context.Menu.SayHelloAsync();

        var message = context.Hints[0].Message;
        Assert.Equal(AiMenuViewModel.MaxMessageLength, message.Length);
        Assert.EndsWith("…", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SayHello_WhenTheModelSaysNothing_ExplainsRatherThanShowingABlank()
    {
        using var context = new Context();
        context.Provider.Reply.Add("  ");

        await context.Menu.SayHelloAsync();

        Assert.Contains("no words", context.Hints[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SayHello_WhenSomethingGoesWrong_ShowsTheReason()
    {
        using var context = new Context();
        context.Provider.Problem = new AiProviderException(AiErrorKind.ModelNotFound, "Ollama does not have that model.");

        await context.Menu.SayHelloAsync();

        Assert.Equal("The rider could not answer", context.Hints[0].Title);
        Assert.Equal("Ollama does not have that model.", context.Hints[0].Message);
        Assert.Contains(context.Menu.BuildEntries(), entry => entry.Text == AiMenuViewModel.SayHelloText);
    }

    [Fact]
    public async Task WhileTheRiderIsReplying_TheMenuOffersToStop_AndStoppingWorks()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["one", "two"]);
        context.Provider.Gate = new TaskCompletionSource();

        var replying = context.Menu.SayHelloAsync();
        await context.Provider.FirstPieceSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var menu = context.Menu.BuildEntries();
        Assert.Contains(menu, entry => entry.Text == AiMenuViewModel.StopReplyText);
        Assert.DoesNotContain(menu, entry => entry.Text == AiMenuViewModel.SayHelloText);

        // Asking again while a reply is in progress does not start a second one.
        await context.Menu.SayHelloAsync();
        Assert.Single(context.Provider.Requests);

        menu.Single(entry => entry.Text == AiMenuViewModel.StopReplyText).Command!.Execute(null);
        await replying.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Reply stopped", Assert.Single(context.Hints).Title);
        Assert.Contains(context.Menu.BuildEntries(), entry => entry.Text == AiMenuViewModel.SayHelloText);
    }

    [Fact]
    public async Task ClosingTheApplicationMidReply_EndsQuietly()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["one", "two"]);
        context.Provider.Gate = new TaskCompletionSource();

        var replying = context.Menu.SayHelloAsync();
        await context.Provider.FirstPieceSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        context.Service.Dispose();
        await replying.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Empty(context.Hints);

        // Using the menu after closing does nothing and does not fail.
        await context.Menu.CheckConnectionAsync();
        await context.Menu.SayHelloAsync();
        Assert.Empty(context.Hints);
    }

    [Fact]
    public async Task TheMenuCommand_RunsTheCheck()
    {
        using var context = new Context(model: null);
        context.Provider.Models.Add(new("alpha"));

        context.Menu.BuildEntries()[0].Command!.Execute(null);
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
            Menu.HintRequested += (_, hint) =>
            {
                Hints.Add(hint);
                HintArrived.TrySetResult();
            };
        }

        public FakeSettingsService Settings { get; } = new();

        public FakeProvider Provider { get; } = new();

        public AiChatService Service { get; }

        public AiMenuViewModel Menu { get; }

        public List<TrayHintEventArgs> Hints { get; } = [];

        public TaskCompletionSource HintArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Dispose()
        {
            Menu.Dispose();
            Service.Dispose();
        }
    }
}
