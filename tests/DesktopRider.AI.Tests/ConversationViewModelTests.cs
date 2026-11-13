using DesktopRider.Core.Configuration;

namespace DesktopRider.AI.Tests;

public class ConversationViewModelTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    [Fact]
    public void AtFirst_IsEmptyAndIdle_AndContactsNothing()
    {
        using var context = new Context();

        Assert.Empty(context.Chat.Lines);
        Assert.Equal(ConversationState.Idle, context.Chat.State);
        Assert.False(context.Chat.IsBusy);
        Assert.Equal(string.Empty, context.Chat.ProcessingText);
        Assert.Equal(ConversationViewModel.NotCheckedText, context.Chat.ConnectionText);
        Assert.False(context.Chat.IsConnected);
        Assert.Equal("picked-model", context.Chat.ModelText);
        Assert.Equal(0, context.Provider.ListCalls);
        Assert.Empty(context.Provider.Requests);
    }

    [Fact]
    public async Task Send_ShowsTheMessageAndTheWholeReply_AndEmptiesTheBox()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["  Braaap! ", "Hello ", "there. \n"]);
        context.Chat.Draft = "  Hi rider  ";

        await context.Chat.SendAsync();

        Assert.Equal(string.Empty, context.Chat.Draft);
        Assert.Equal(
            [(Speaker.User, "Hi rider"), (Speaker.Rider, "Braaap! Hello there.")],
            context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
        Assert.Equal("Braaap! Hello there.", context.Chat.CurrentReply);
        Assert.Equal(ConversationState.Idle, context.Chat.State);
        Assert.True(context.Chat.IsConnected);
        Assert.Equal(ConversationViewModel.ConnectedText, context.Chat.ConnectionText);
        Assert.Equal("Hi rider", context.Provider.Requests[0].Messages[^1].Content);
    }

    [Fact]
    public async Task Send_GoesFromThinkingToTalkingToIdle()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["one ", "two"]);
        var states = new List<ConversationState>();
        var indicator = new List<string>();
        context.Chat.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConversationViewModel.State))
            {
                states.Add(context.Chat.State);
                indicator.Add(context.Chat.ProcessingText);
            }
        };
        context.Chat.Draft = "Hello";

        await context.Chat.SendAsync();

        Assert.Equal([ConversationState.Thinking, ConversationState.Talking, ConversationState.Idle], states);
        Assert.Equal([ConversationViewModel.ThinkingText, ConversationViewModel.TalkingText, string.Empty], indicator);
    }

    [Fact]
    public async Task TheReplyGrowsInPlace_AsItArrives()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["one ", "two ", "three"]);
        var seen = new List<string>();
        context.Chat.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConversationViewModel.CurrentReply) && context.Chat.CurrentReply.Length > 0)
            {
                seen.Add(context.Chat.CurrentReply);
            }
        };
        context.Chat.Draft = "Count";

        await context.Chat.SendAsync();

        Assert.Equal(["one ", "one two ", "one two three"], seen);
        Assert.Equal(2, context.Chat.Lines.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n ")]
    public async Task Send_WithNothingTyped_DoesNothing(string draft)
    {
        using var context = new Context();
        context.Chat.Draft = draft;

        await context.Chat.SendAsync();

        Assert.Empty(context.Chat.Lines);
        Assert.Empty(context.Provider.Requests);
    }

    [Fact]
    public async Task Send_WithAVeryLongMessage_ShortensIt()
    {
        using var context = new Context();
        context.Provider.Reply.Add("ok");
        context.Chat.Draft = new string('a', ConversationViewModel.MaxMessageLength + 500);

        await context.Chat.SendAsync();

        Assert.Equal(ConversationViewModel.MaxMessageLength, context.Chat.Lines[0].Text.Length);
    }

    [Fact]
    public async Task LaterMessages_CarryTheConversationSoFar()
    {
        using var context = new Context();
        context.Provider.Reply.Add("First answer");
        context.Chat.Draft = "First question";
        await context.Chat.SendAsync();
        context.Chat.Draft = "Second question";

        await context.Chat.SendAsync();

        var sent = context.Provider.Requests[1].Messages;
        Assert.Equal(
            [(ChatRole.System, RiderPersonality.Compose(new AiSettings())), (ChatRole.User, "First question"), (ChatRole.Assistant, "First answer"), (ChatRole.User, "Second question")],
            sent.Select(message => (message.Role, message.Content)));
    }

    [Fact]
    public async Task ProblemsAreExplainedInTheConversation_ButNeverSentToTheAi()
    {
        using var context = new Context();
        context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");
        var notices = new List<string>();
        context.Chat.NoticeAdded += (_, notice) => notices.Add(notice.Message);
        context.Chat.Draft = "Hello?";

        await context.Chat.SendAsync();

        Assert.Equal(
            [(Speaker.User, "Hello?"), (Speaker.Notice, "Desktop Rider cannot reach Ollama.")],
            context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
        Assert.Equal(["Desktop Rider cannot reach Ollama."], notices);
        Assert.Equal(ConversationState.Idle, context.Chat.State);
        Assert.False(context.Chat.IsConnected);
        Assert.Equal(ConversationViewModel.NotConnectedText, context.Chat.ConnectionText);

        // Ollama comes back: the next message works and the explanation is left out of what is sent.
        context.Provider.Problem = null;
        context.Provider.Reply.Add("Here now!");
        context.Chat.Draft = "Again";
        await context.Chat.SendAsync();

        Assert.Equal("Here now!", context.Chat.Lines[^1].Text);
        Assert.True(context.Chat.IsConnected);
        Assert.DoesNotContain(context.Provider.Requests[^1].Messages, message => message.Content.Contains("cannot reach", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Send_WithNoModelChosen_SaysWhatToDo()
    {
        using var context = new Context(model: null);
        context.Chat.Draft = "Hello";

        await context.Chat.SendAsync();

        Assert.Equal(Speaker.Notice, context.Chat.Lines[^1].Speaker);
        Assert.Equal(ConversationViewModel.ChooseModelAdvice, context.Chat.Advice);
        Assert.True(context.Chat.HasAdvice);
        Assert.Equal(ConversationViewModel.NoModelText, context.Chat.ModelText);
        Assert.Empty(context.Provider.Requests);
    }

    [Fact]
    public async Task Send_WhenTheModelHasGone_AsksForAnother()
    {
        using var context = new Context();
        context.Provider.Problem = new AiProviderException(AiErrorKind.ModelNotFound, "Ollama does not have that model.");
        context.Chat.Draft = "Hello";

        await context.Chat.SendAsync();

        Assert.Equal(ConversationViewModel.ModelGoneAdvice, context.Chat.Advice);
    }

    [Fact]
    public async Task Send_WhenTheModelSaysNothing_ExplainsRatherThanShowingABlank()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["  ", "\n"]);
        context.Chat.Draft = "Hello";

        await context.Chat.SendAsync();

        Assert.Equal(
            [(Speaker.User, "Hello"), (Speaker.Notice, ConversationViewModel.EmptyReplyNotice)],
            context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
    }

    [Fact]
    public async Task Stop_KeepsWhatArrived_AndSaysTheReplyWasStopped()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["one", "two"]);
        context.Provider.Gate = new TaskCompletionSource();
        context.Chat.Draft = "Go";

        var sending = context.Chat.SendAsync();
        await context.Provider.FirstPieceSent.Task.WaitAsync(Patience);
        Assert.True(context.Chat.IsBusy);

        // Sending again while a reply is on its way does not start a second one.
        context.Chat.Draft = "And another";
        await context.Chat.SendAsync();
        Assert.Single(context.Provider.Requests);
        Assert.Equal("And another", context.Chat.Draft);

        context.Chat.StopCommand.Execute(null);
        await sending.WaitAsync(Patience);

        Assert.Equal(
            [(Speaker.User, "Go"), (Speaker.Rider, "one"), (Speaker.Notice, ConversationViewModel.StoppedNotice)],
            context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
        Assert.False(context.Chat.IsBusy);
    }

    [Fact]
    public async Task Clear_EmptiesTheConversation_AndStopsAReplyQuietly()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["one", "two"]);
        context.Provider.Gate = new TaskCompletionSource();
        context.Chat.Draft = "Go";
        var sending = context.Chat.SendAsync();
        await context.Provider.FirstPieceSent.Task.WaitAsync(Patience);

        context.Chat.ClearCommand.Execute(null);
        await sending.WaitAsync(Patience);

        Assert.Empty(context.Chat.Lines);
        Assert.Equal(string.Empty, context.Chat.CurrentReply);
        Assert.Equal(ConversationState.Idle, context.Chat.State);

        // A fresh conversation starts from nothing.
        context.Provider.Gate = null;
        context.Chat.Draft = "New start";
        await context.Chat.SendAsync();
        Assert.Equal([ChatRole.System, ChatRole.User], context.Provider.Requests[^1].Messages.Select(message => message.Role));
    }

    [Fact]
    public async Task ClosingTheApplicationMidReply_EndsQuietly()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["one", "two"]);
        context.Provider.Gate = new TaskCompletionSource();
        context.Chat.Draft = "Go";
        var sending = context.Chat.SendAsync();
        await context.Provider.FirstPieceSent.Task.WaitAsync(Patience);

        context.Service.Dispose();
        await sending.WaitAsync(Patience);

        Assert.DoesNotContain(context.Chat.Lines, line => line.Speaker == Speaker.Notice);
        Assert.False(context.Chat.IsBusy);

        // Using the conversation after closing does nothing and does not fail.
        context.Chat.Draft = "Still there?";
        await context.Chat.SendAsync();
        await context.Chat.CheckConnectionAsync();
        Assert.False(context.Chat.IsBusy);
    }

    [Fact]
    public async Task OnlyTheNewestLinesAreSent_SoALongChatStaysQuick()
    {
        using var context = new Context();
        context.Provider.Reply.Add("ok");
        for (var i = 0; i < ConversationViewModel.MaxLinesSent; i++)
        {
            context.Chat.Draft = $"message {i}";
            await context.Chat.SendAsync();
        }

        var sent = context.Provider.Requests[^1].Messages;
        Assert.Equal(ConversationViewModel.MaxLinesSent + 1, sent.Count);
        Assert.Equal(ChatRole.System, sent[0].Role);
        Assert.Equal($"message {ConversationViewModel.MaxLinesSent - 1}", sent[^1].Content);
    }

    [Fact]
    public async Task Check_ListsTheModels_AndShowsTheChosenOne()
    {
        using var context = new Context(model: "beta");
        context.Provider.Models.AddRange([new("alpha"), new("beta")]);

        await context.Chat.CheckConnectionAsync();

        Assert.Equal(["alpha", "beta"], context.Chat.Models);
        Assert.Equal("beta", context.Chat.SelectedModel);
        Assert.True(context.Chat.IsConnected);
        Assert.Equal(ConversationViewModel.ConnectedText, context.Chat.ConnectionText);
        Assert.False(context.Chat.HasAdvice);
    }

    [Fact]
    public async Task Check_WithNoModelChosen_AsksForOne_AndChoosingClearsTheAdvice()
    {
        using var context = new Context(model: null);
        context.Provider.Models.AddRange([new("alpha"), new("beta")]);
        await context.Chat.CheckConnectionAsync();
        Assert.Equal(ConversationViewModel.ChooseModelAdvice, context.Chat.Advice);
        var changed = new List<string?>();
        context.Chat.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        context.Chat.SelectedModel = "alpha";

        Assert.Equal("alpha", context.Settings.Current.Ai.Model);
        Assert.Equal("alpha", context.Chat.SelectedModel);
        Assert.Equal("alpha", context.Chat.ModelText);
        Assert.False(context.Chat.HasAdvice);
        Assert.Contains(nameof(ConversationViewModel.ModelText), changed);
    }

    [Fact]
    public async Task Check_WhenTheChosenModelHasGone_AsksForAnother()
    {
        using var context = new Context(model: "gone");
        context.Provider.Models.Add(new("alpha"));

        await context.Chat.CheckConnectionAsync();

        Assert.Equal(ConversationViewModel.ModelGoneAdvice, context.Chat.Advice);
    }

    [Fact]
    public async Task Check_WithNoModelsInstalled_ExplainsWhatToDo()
    {
        using var context = new Context();

        await context.Chat.CheckConnectionAsync();

        Assert.True(context.Chat.IsConnected);
        Assert.Equal(ConversationViewModel.NoModelsAdvice, context.Chat.Advice);
        Assert.Empty(context.Chat.Models);
    }

    [Fact]
    public async Task Check_WhenOllamaIsNotRunning_ShowsTheReason_AndRecoversNextTime()
    {
        using var context = new Context();
        context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");

        await context.Chat.CheckConnectionAsync();

        Assert.False(context.Chat.IsConnected);
        Assert.Equal(ConversationViewModel.NotConnectedText, context.Chat.ConnectionText);
        Assert.Equal("Desktop Rider cannot reach Ollama.", context.Chat.Advice);
        Assert.Empty(context.Chat.Lines);

        context.Provider.Problem = null;
        context.Provider.Models.Add(new("picked-model"));
        context.Chat.CheckConnectionCommand.Execute(null);

        Assert.True(context.Chat.IsConnected);
        Assert.False(context.Chat.HasAdvice);
    }

    [Fact]
    public async Task ChoosingAModel_WhenItCannotBeSaved_SaysSo()
    {
        using var context = new Context(model: null);
        context.Settings.FailSaves = true;

        await context.Chat.SelectModelAsync("alpha");

        Assert.Equal(ConversationViewModel.SaveFailedAdvice, context.Chat.Advice);
        Assert.Null(context.Chat.SelectedModel);
    }

    [Fact]
    public void ChoosingTheSameModelOrNothing_SavesNothing()
    {
        using var context = new Context();

        context.Chat.SelectedModel = "picked-model";
        context.Chat.SelectedModel = null;
        context.Chat.SelectedModel = " ";

        Assert.Equal(0, context.Settings.SaveCount);
        Assert.Equal("picked-model", context.Chat.SelectedModel);
    }

    [Fact]
    public async Task TheSendCommand_SendsWhatWasTyped()
    {
        using var context = new Context();
        context.Provider.Reply.Add("Hi!");
        context.Chat.Draft = "Hello";

        context.Chat.SendCommand.Execute(null);
        await WaitUntilAsync(() => !context.Chat.IsBusy && context.Chat.Lines.Count == 2);

        Assert.Equal("Hi!", context.Chat.Lines[1].Text);
    }

    [Fact]
    public async Task OldLinesAreDropped_SoTheWindowNeverGrowsWithoutLimit()
    {
        using var context = new Context();
        context.Provider.Reply.Add("ok");
        for (var i = 0; i < (ConversationViewModel.MaxLinesKept / 2) + 3; i++)
        {
            context.Chat.Draft = $"message {i}";
            await context.Chat.SendAsync();
        }

        Assert.Equal(ConversationViewModel.MaxLinesKept, context.Chat.Lines.Count);
        Assert.Equal("ok", context.Chat.Lines[^1].Text);
    }

    [Fact]
    public async Task ATagFromTheAi_MakesTheRiderDoIt_AndIsNeverShown()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["[whee", "lie] Hold ", "tight!"]);
        var actions = Actions(context);
        context.Chat.Draft = "Show me something cool";

        await context.Chat.SendAsync();

        Assert.Equal(["Wheelie"], actions);
        Assert.Equal("Hold tight!", context.Chat.Lines[^1].Text);
        Assert.Equal("Hold tight!", context.Chat.CurrentReply);
    }

    [Fact]
    public async Task APlainRequest_MakesTheRiderDoIt_EvenWhenTheAiForgetsTheTag()
    {
        using var context = new Context();
        context.Provider.Reply.Add("Sure thing, watch this!");
        var actions = Actions(context);
        context.Chat.Draft = "Can you do a wheelie?";

        await context.Chat.SendAsync();

        Assert.Equal(["Wheelie"], actions);
    }

    [Fact]
    public async Task ARequestAndATagForTheSameThing_AreDoneOnce()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["[wheelie] Here ", "it comes! [wheelie]"]);
        var actions = Actions(context);
        context.Chat.Draft = "Do a wheelie";

        await context.Chat.SendAsync();

        Assert.Equal(["Wheelie"], actions);
        Assert.Equal("Here it comes!", context.Chat.Lines[^1].Text);
    }

    [Fact]
    public async Task ARequestThatCannotBeAnswered_DoesNothing()
    {
        using var context = new Context();
        context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");
        var actions = Actions(context);
        context.Chat.Draft = "Do a wheelie";

        await context.Chat.SendAsync();

        Assert.Empty(actions);
    }

    [Fact]
    public async Task OrdinaryBrackets_StayInTheReply()
    {
        using var context = new Context();
        context.Provider.Reply.AddRange(["Use a list [1, 2] or ", "an [unfinished"]);
        var actions = Actions(context);
        context.Chat.Draft = "Tell me about lists";

        await context.Chat.SendAsync();

        Assert.Empty(actions);
        Assert.Equal("Use a list [1, 2] or an [unfinished", context.Chat.Lines[^1].Text);
    }

    [Fact]
    public async Task TheAiIsToldWhatTheRiderCanDo()
    {
        using var context = new Context();
        context.Provider.Reply.Add("ok");
        context.Chat.Draft = "Hello";

        await context.Chat.SendAsync();

        var instructions = context.Provider.Requests[0].Messages[0].Content;
        Assert.All(RiderActions.All, action => Assert.Contains($"[{action.Tag}]", instructions, StringComparison.Ordinal));
    }

    private static List<string> Actions(Context context)
    {
        var actions = new List<string>();
        context.Chat.ActionRequested += (_, e) => actions.Add(e.Action.Animation);
        return actions;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The expected result did not arrive in time.");
            await Task.Delay(10);
        }
    }

    internal sealed class Context : IDisposable
    {
        public Context(string? model = "picked-model")
        {
            Settings.Current.Ai.Model = model;
            Memory = new RiderMemory(MemoryStore, Settings, Clock);
            Service = new AiChatService(new AiProviderRegistry([Provider]), Settings, Memory);
            Chat = new ConversationViewModel(Service, Settings, store: Store, timeProvider: Clock, memory: Memory);
        }

        public FakeMemoryStore MemoryStore { get; } = new();

        public RiderMemory Memory { get; }

        public FakeConversationStore Store { get; } = new();

        public ManualClock Clock { get; } = new();

        public FakeSettingsService Settings { get; } = new();

        public FakeProvider Provider { get; } = new();

        public AiChatService Service { get; }

        public ConversationViewModel Chat { get; }

        public void Dispose()
        {
            Chat.Dispose();
            Service.Dispose();
        }
    }
}
