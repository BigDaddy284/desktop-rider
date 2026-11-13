#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DesktopRider.Core.Configuration;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    public class ConversationViewModelTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

        [Test]
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

        [Test]
        public async Task Send_ShowsTheMessageAndTheWholeReply_AndEmptiesTheBox()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "  Braaap! ", "Hello ", "there. \n" });
            context.Chat.Draft = "  Hi rider  ";

            await context.Chat.SendAsync();

            Assert.Equal(string.Empty, context.Chat.Draft);
            Assert.Equal(
                new[] { (Speaker.User, "Hi rider"), (Speaker.Rider, "Braaap! Hello there.") },
                context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
            Assert.Equal("Braaap! Hello there.", context.Chat.CurrentReply);
            Assert.Equal(ConversationState.Idle, context.Chat.State);
            Assert.True(context.Chat.IsConnected);
            Assert.Equal(ConversationViewModel.ConnectedText, context.Chat.ConnectionText);
            Assert.Equal("Hi rider", context.Provider.Requests[0].Messages[^1].Content);
        }

        [Test]
        public async Task Send_GoesFromThinkingToTalkingToIdle()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "one ", "two" });
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

            Assert.Equal(new[] { ConversationState.Thinking, ConversationState.Talking, ConversationState.Idle }, states);
            Assert.Equal(new[] { ConversationViewModel.ThinkingText, ConversationViewModel.TalkingText, string.Empty }, indicator);
        }

        [Test]
        public async Task TheReplyGrowsInPlace_AsItArrives()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "one ", "two ", "three" });
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

            Assert.Equal(new[] { "one ", "one two ", "one two three" }, seen);
            Assert.Equal(2, context.Chat.Lines.Count);
        }
        [TestCase("")]
        [TestCase("   \r\n ")]
        public async Task Send_WithNothingTyped_DoesNothing(string draft)
        {
            using var context = new Context();
            context.Chat.Draft = draft;

            await context.Chat.SendAsync();

            Assert.Empty(context.Chat.Lines);
            Assert.Empty(context.Provider.Requests);
        }

        [Test]
        public async Task Send_WithAVeryLongMessage_ShortensIt()
        {
            using var context = new Context();
            context.Provider.Reply.Add("ok");
            context.Chat.Draft = new string('a', ConversationViewModel.MaxMessageLength + 500);

            await context.Chat.SendAsync();

            Assert.Equal(ConversationViewModel.MaxMessageLength, context.Chat.Lines[0].Text.Length);
        }

        [Test]
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
                new[] { (ChatRole.System, RiderPersonality.Compose(new AiSettings())), (ChatRole.User, "First question"), (ChatRole.Assistant, "First answer"), (ChatRole.User, "Second question") },
                sent.Select(message => (message.Role, message.Content)));
        }

        [Test]
        public async Task ProblemsAreExplainedInTheConversation_ButNeverSentToTheAi()
        {
            using var context = new Context();
            context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");
            var notices = new List<string>();
            context.Chat.NoticeAdded += (_, notice) => notices.Add(notice.Message);
            context.Chat.Draft = "Hello?";

            await context.Chat.SendAsync();

            Assert.Equal(
                new[] { (Speaker.User, "Hello?"), (Speaker.Notice, "Desktop Rider cannot reach Ollama.") },
                context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
            Assert.Equal(new[] { "Desktop Rider cannot reach Ollama." }, notices);
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

        [Test]
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

        [Test]
        public async Task Send_WhenTheModelHasGone_AsksForAnother()
        {
            using var context = new Context();
            context.Provider.Problem = new AiProviderException(AiErrorKind.ModelNotFound, "Ollama does not have that model.");
            context.Chat.Draft = "Hello";

            await context.Chat.SendAsync();

            Assert.Equal(ConversationViewModel.ModelGoneAdvice, context.Chat.Advice);
        }

        [Test]
        public async Task Send_WhenTheModelSaysNothing_ExplainsRatherThanShowingABlank()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "  ", "\n" });
            context.Chat.Draft = "Hello";

            await context.Chat.SendAsync();

            Assert.Equal(
                new[] { (Speaker.User, "Hello"), (Speaker.Notice, ConversationViewModel.EmptyReplyNotice) },
                context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
        }

        [Test]
        public async Task Stop_KeepsWhatArrived_AndSaysTheReplyWasStopped()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "one", "two" });
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
                new[] { (Speaker.User, "Go"), (Speaker.Rider, "one"), (Speaker.Notice, ConversationViewModel.StoppedNotice) },
                context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
            Assert.False(context.Chat.IsBusy);
        }

        [Test]
        public async Task Clear_EmptiesTheConversation_AndStopsAReplyQuietly()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "one", "two" });
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
            Assert.Equal(new[] { ChatRole.System, ChatRole.User }, context.Provider.Requests[^1].Messages.Select(message => message.Role));
        }

        [Test]
        public async Task ClosingTheApplicationMidReply_EndsQuietly()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "one", "two" });
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

        [Test]
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

        [Test]
        public async Task Check_ListsTheModels_AndShowsTheChosenOne()
        {
            using var context = new Context(model: "beta");
            context.Provider.Models.AddRange(new AiModelInfo[] { new("alpha"), new("beta") });

            await context.Chat.CheckConnectionAsync();

            Assert.Equal(new[] { "alpha", "beta" }, context.Chat.Models);
            Assert.Equal("beta", context.Chat.SelectedModel);
            Assert.True(context.Chat.IsConnected);
            Assert.Equal(ConversationViewModel.ConnectedText, context.Chat.ConnectionText);
            Assert.False(context.Chat.HasAdvice);
        }

        [Test]
        public async Task Check_WithNoModelChosen_AsksForOne_AndChoosingClearsTheAdvice()
        {
            using var context = new Context(model: null);
            context.Provider.Models.AddRange(new AiModelInfo[] { new("alpha"), new("beta") });
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

        [Test]
        public async Task Check_WhenTheChosenModelHasGone_AsksForAnother()
        {
            using var context = new Context(model: "gone");
            context.Provider.Models.Add(new("alpha"));

            await context.Chat.CheckConnectionAsync();

            Assert.Equal(ConversationViewModel.ModelGoneAdvice, context.Chat.Advice);
        }

        [Test]
        public async Task Check_WithNoModelsInstalled_ExplainsWhatToDo()
        {
            using var context = new Context();

            await context.Chat.CheckConnectionAsync();

            Assert.True(context.Chat.IsConnected);
            Assert.Equal(ConversationViewModel.NoModelsAdvice, context.Chat.Advice);
            Assert.Empty(context.Chat.Models);
        }

        [Test]
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

        [Test]
        public async Task ChoosingAModel_WhenItCannotBeSaved_SaysSo()
        {
            using var context = new Context(model: null);
            context.Settings.FailSaves = true;

            await context.Chat.SelectModelAsync("alpha");

            Assert.Equal(ConversationViewModel.SaveFailedAdvice, context.Chat.Advice);
            Assert.Null(context.Chat.SelectedModel);
        }

        [Test]
        public void ChoosingTheSameModelOrNothing_SavesNothing()
        {
            using var context = new Context();

            context.Chat.SelectedModel = "picked-model";
            context.Chat.SelectedModel = null;
            context.Chat.SelectedModel = " ";

            Assert.Equal(0, context.Settings.SaveCount);
            Assert.Equal("picked-model", context.Chat.SelectedModel);
        }

        [Test]
        public async Task TheSendCommand_SendsWhatWasTyped()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Hi!");
            context.Chat.Draft = "Hello";

            context.Chat.SendCommand.Execute(null);
            await WaitUntilAsync(() => !context.Chat.IsBusy && context.Chat.Lines.Count == 2);

            Assert.Equal("Hi!", context.Chat.Lines[1].Text);
        }

        [Test]
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

        [Test]
        public async Task ATagFromTheAi_MakesTheRiderDoIt_AndIsNeverShown()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "[whee", "lie] Hold ", "tight!" });
            var actions = Actions(context);
            context.Chat.Draft = "Show me something cool";

            await context.Chat.SendAsync();

            Assert.Equal(new[] { "Wheelie" }, actions);
            Assert.Equal("Hold tight!", context.Chat.Lines[^1].Text);
            Assert.Equal("Hold tight!", context.Chat.CurrentReply);
        }

        [Test]
        public async Task APlainRequest_MakesTheRiderDoIt_EvenWhenTheAiForgetsTheTag()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Sure thing, watch this!");
            var actions = Actions(context);
            context.Chat.Draft = "Can you do a wheelie?";

            await context.Chat.SendAsync();

            Assert.Equal(new[] { "Wheelie" }, actions);
        }

        [Test]
        public async Task ARequestAndATagForTheSameThing_AreDoneOnce()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "[wheelie] Here ", "it comes! [wheelie]" });
            var actions = Actions(context);
            context.Chat.Draft = "Do a wheelie";

            await context.Chat.SendAsync();

            Assert.Equal(new[] { "Wheelie" }, actions);
            Assert.Equal("Here it comes!", context.Chat.Lines[^1].Text);
        }

        [Test]
        public async Task ARequestThatCannotBeAnswered_DoesNothing()
        {
            using var context = new Context();
            context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");
            var actions = Actions(context);
            context.Chat.Draft = "Do a wheelie";

            await context.Chat.SendAsync();

            Assert.Empty(actions);
        }

        [Test]
        public async Task OrdinaryBrackets_StayInTheReply()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "Use a list [1, 2] or ", "an [unfinished" });
            var actions = Actions(context);
            context.Chat.Draft = "Tell me about lists";

            await context.Chat.SendAsync();

            Assert.Empty(actions);
            Assert.Equal("Use a list [1, 2] or an [unfinished", context.Chat.Lines[^1].Text);
        }

        [Test]
        public async Task TheAiIsToldWhatTheRiderCanDo()
        {
            using var context = new Context();
            context.Provider.Reply.Add("ok");
            context.Chat.Draft = "Hello";

            await context.Chat.SendAsync();

            var instructions = context.Provider.Requests[0].Messages[0].Content;
            Assert.All(RiderActions.All, action => Assert.Contains($"[{action.Tag}]", instructions, StringComparison.Ordinal));
        }
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public async Task AskedForSomeTricks_TheRiderDoesAFewDifferentOnes_AndOnlyTricks(int seed)
        {
            using var context = new Context(seed: seed);
            context.Provider.Reply.Add("You got it, hold on tight!");
            var actions = Actions(context);
            context.Chat.Draft = "Do some tricks";

            await context.Chat.SendAsync();

            var tricks = RiderActions.Tricks.Select(action => action.Animation).ToList();
            Assert.InRange(actions.Count, RiderActions.FewestInARow, RiderActions.MostInARow);
            Assert.Equal(actions.Count, actions.Distinct().Count());
            Assert.All(actions, animation => Assert.Contains(animation, tricks));
        }

        [Test]
        public async Task AskedForSomeTricks_TheTrickTheRiderNames_IsDoneFirst_ThenMore()
        {
            // What happened to the owner: the rider said "how about a no hander" and then did a single wheelie.
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "Ok, how about ", "a no hander?" });
            var actions = Actions(context);
            context.Chat.Draft = "do some tricks";

            await context.Chat.SendAsync();

            Assert.Equal("NoHander", actions[0]);
            Assert.InRange(actions.Count, RiderActions.FewestInARow, RiderActions.MostInARow);
            Assert.Equal(actions.Count, actions.Distinct().Count());
            Assert.Equal("Ok, how about a no hander?", context.Chat.Lines[^1].Text);
        }

        [Test]
        public async Task AskedForATrick_TheOneTheRiderNames_IsTheOneDone()
        {
            using var context = new Context();
            context.Provider.Reply.Add("One superman coming up!");
            var actions = Actions(context);
            context.Chat.Draft = "Do a trick";

            await context.Chat.SendAsync();

            Assert.Equal(new[] { "Superman" }, actions);
        }

        [Test]
        public async Task AskedForATrick_AndTheRiderNamesNone_OneIsPickedAtRandom()
        {
            using var context = new Context();
            context.Provider.Reply.Add("Watch this!");
            var actions = Actions(context);
            context.Chat.Draft = "Show me a trick";

            await context.Chat.SendAsync();

            Assert.Contains(Assert.Single(actions), RiderActions.Tricks.Select(action => action.Animation));
        }

        [Test]
        public async Task TheTricksTag_FromTheAi_DoesAFewTricks_AndIsNeverShown()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "[tri", "cks] Here we go!" });
            var actions = Actions(context);
            context.Chat.Draft = "I'm bored, entertain me";

            await context.Chat.SendAsync();

            Assert.InRange(actions.Count, RiderActions.FewestInARow, RiderActions.MostInARow);
            Assert.DoesNotContain(string.Empty, actions);
            Assert.Equal("Here we go!", context.Chat.Lines[^1].Text);
        }

        [Test]
        public async Task SeveralTagsFromTheAi_AreDoneInOrder_AndCountTowardsTheFew()
        {
            using var context = new Context();
            context.Provider.Reply.Add("[backflip] [whip] [superman] Three for you!");
            var actions = Actions(context);
            context.Chat.Draft = "do some tricks";

            await context.Chat.SendAsync();

            Assert.Equal(new[] { "Backflip", "Whip", "Superman" }, actions.Take(3));
            Assert.InRange(actions.Count, 3, RiderActions.MostInARow);
        }

        [Test]
        public async Task TheAiCannotMakeTheRiderDoMoreThanTheLimit()
        {
            using var context = new Context();
            context.Provider.Reply.Add(string.Concat(RiderActions.Tricks.Select(action => $"[{action.Tag}]")) + " Phew!");
            var actions = Actions(context);
            context.Chat.Draft = "Hello";

            await context.Chat.SendAsync();

            Assert.Equal(ConversationViewModel.MaxActionsPerReply, actions.Count);
            Assert.Equal("Phew!", context.Chat.Lines[^1].Text);
        }

        [Test]
        public async Task TalkAboutATrick_WithoutBeingAskedForOne_DoesNothing()
        {
            using var context = new Context();
            context.Provider.Reply.Add("A backflip is a full rotation backwards off the ramp.");
            var actions = Actions(context);
            context.Chat.Draft = "What is a backflip?";

            await context.Chat.SendAsync();

            Assert.Empty(actions);
        }

        [Test]
        public async Task AskedForSomeTricks_ButTheAiCannotAnswer_NothingIsDone()
        {
            using var context = new Context();
            context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");
            var actions = Actions(context);
            context.Chat.Draft = "Do some tricks";

            await context.Chat.SendAsync();

            Assert.Empty(actions);
        }
        [TestCase("Okay! Let's get back to those tricks \u2013 fancy a [backflip]?", "Okay! Let's get back to those tricks \u2013 fancy a backflip?")]
        [TestCase("[seat-grab] \u2013 A superman seat grab it is!", "A superman seat grab it is!")]
        [TestCase("[tricks] - Let's go!", "Let's go!")]
        [TestCase("\u2013 Let's go for a [tricks]!", "Let's go for a few tricks!")]
        [TestCase("[whip]: Sure, watch this!", "Sure, watch this!")]
        public async Task TakingATagOut_NeverLeavesAHoleOrAStrayDashInWhatTheRiderSays(string reply, string shown)
        {
            // What the owner saw with 0.8.0: "fancy a ?", "Let's go for a !" and "- A superman seat grab it is!".
            using var context = new Context();
            context.Provider.Reply.Add(reply);
            var actions = Actions(context);
            context.Chat.Draft = "show me some tricks";

            await context.Chat.SendAsync();

            Assert.Equal(shown, context.Chat.Lines[^1].Text);
            Assert.Equal(shown, context.Chat.CurrentReply);
            Assert.NotEmpty(actions);
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
            public Context(string? model = "picked-model", int seed = 1)
            {
                Settings.Current.Ai.Model = model;
                Memory = new RiderMemory(MemoryStore, Settings, Clock);
                Service = new AiChatService(new AiProviderRegistry(new[] { Provider }), Settings, Memory);
                Chat = new ConversationViewModel(Service, Settings, store: Store, timeProvider: Clock, memory: Memory, random: new Random(seed));
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
}
