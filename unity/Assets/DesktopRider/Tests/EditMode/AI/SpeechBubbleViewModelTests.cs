#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    public class SpeechBubbleViewModelTests
    {
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

        [Test]
        public void AtFirst_NothingIsShown()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);

            Assert.False(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);
        }

        [Test]
        public async Task WithTheConversationClosed_ShowsThinking_ThenTheReplyGrowing_ThenLeavesItToBeRead()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            context.Provider.Reply.AddRange(new[] { "Braaap! ", "Hello." });
            var said = new List<(string Text, bool Stays)>();
            bubble.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SpeechBubbleViewModel.Text))
                {
                    said.Add((bubble.Text, bubble.HideAfter is null));
                }
            };
            context.Chat.Draft = "Hi";

            await context.Chat.SendAsync();

            Assert.Equal(new[] { (SpeechBubbleViewModel.ThinkingText, true), ("Braaap! ", true), ("Braaap! Hello.", true) }, said);
            Assert.True(bubble.IsVisible);
            Assert.Equal("Braaap! Hello.", bubble.Text);
            Assert.Equal(SpeechBubbleViewModel.ReadingTime("Braaap! Hello."), bubble.HideAfter);
        }

        [Test]
        public async Task WithTheConversationOpen_StaysAway()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat) { ConversationOpen = true };
            context.Provider.Reply.Add("Hello.");
            var shown = false;
            bubble.PropertyChanged += (_, _) => shown |= bubble.IsVisible;
            context.Chat.Draft = "Hi";

            await context.Chat.SendAsync();

            Assert.False(shown);

            // A problem is already explained in the open window too.
            context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");
            context.Chat.Draft = "Hi again";
            await context.Chat.SendAsync();
            Assert.False(shown);
        }

        [Test]
        public async Task ClosingTheConversationMidReply_HandsTheRestToTheBubble_AndOpeningItTakesTheBubbleAway()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat) { ConversationOpen = true };
            context.Provider.Reply.AddRange(new[] { "one ", "two" });
            context.Provider.Gate = new TaskCompletionSource();
            context.Chat.Draft = "Go";
            var sending = context.Chat.SendAsync();
            await context.Provider.FirstPieceSent.Task.WaitAsync(Patience);

            bubble.ConversationOpen = false;

            Assert.True(bubble.IsVisible);
            Assert.Equal("one ", bubble.Text);
            Assert.Null(bubble.HideAfter);

            context.Provider.Gate.SetResult();
            await sending.WaitAsync(Patience);
            Assert.Equal("one two", bubble.Text);
            Assert.NotNull(bubble.HideAfter);

            bubble.ConversationOpen = true;
            Assert.False(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);
        }

        [Test]
        public async Task AProblem_IsShownInTheBubble_AndLeftToBeRead()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Desktop Rider cannot reach Ollama.");
            context.Chat.Draft = "Hi";

            await context.Chat.SendAsync();

            Assert.True(bubble.IsVisible);
            Assert.Equal("Desktop Rider cannot reach Ollama.", bubble.Text);
            Assert.NotNull(bubble.HideAfter);
        }

        [Test]
        public async Task Dismiss_TakesTheBubbleAway()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            context.Provider.Reply.Add("Hello.");
            context.Chat.Draft = "Hi";
            await context.Chat.SendAsync();

            bubble.Dismiss();

            Assert.False(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);
        }

        [Test]
        public async Task ClearingTheConversation_TakesTheBubbleAway()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            context.Provider.Reply.Add("Hello.");
            context.Chat.Draft = "Hi";
            await context.Chat.SendAsync();
            Assert.True(bubble.IsVisible);

            context.Chat.Clear();

            Assert.False(bubble.IsVisible);
        }

        [Test]
        public async Task ALongReply_ShowsItsNewestWordsWhileArriving_AndItsStartOnceFinished()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            var longReply = string.Join(' ', Enumerable.Range(0, 200).Select(i => $"word{i}"));
            context.Provider.Reply.AddRange(new[] { longReply, " END" });
            context.Provider.Gate = new TaskCompletionSource();
            context.Chat.Draft = "Tell me lots";
            var sending = context.Chat.SendAsync();
            await context.Provider.FirstPieceSent.Task.WaitAsync(Patience);

            Assert.StartsWith("…", bubble.Text, StringComparison.Ordinal);
            Assert.EndsWith("word199", bubble.Text, StringComparison.Ordinal);
            Assert.True(bubble.Text.Length <= SpeechBubbleViewModel.MaxLength);

            context.Provider.Gate.SetResult();
            await sending.WaitAsync(Patience);

            Assert.StartsWith("word0 word1 ", bubble.Text, StringComparison.Ordinal);
            Assert.EndsWith("…", bubble.Text, StringComparison.Ordinal);
            Assert.True(bubble.Text.Length <= SpeechBubbleViewModel.MaxLength);
            Assert.Equal(longReply + " END", context.Chat.Lines[^1].Text);
        }

        [Test]
        public void ShortText_IsShownWhole()
        {
            Assert.Equal("Hello there.", SpeechBubbleViewModel.FirstPart("Hello there."));
            Assert.Equal("Hello there.", SpeechBubbleViewModel.NewestPart("Hello there."));
        }

        [Test]
        public void TextWithNoSpaces_IsStillShortenedToFit()
        {
            var text = new string('a', 1000);

            Assert.Equal(SpeechBubbleViewModel.MaxLength, SpeechBubbleViewModel.FirstPart(text).Length);
            Assert.Equal(SpeechBubbleViewModel.MaxLength, SpeechBubbleViewModel.NewestPart(text).Length);
        }

        [Test]
        public void ReadingTime_GrowsWithLength_WithinSensibleLimits()
        {
            Assert.Equal(SpeechBubbleViewModel.ShortestStay, SpeechBubbleViewModel.ReadingTime("Hi"));
            Assert.Equal(SpeechBubbleViewModel.LongestStay, SpeechBubbleViewModel.ReadingTime(new string('a', 5000)));
            Assert.True(SpeechBubbleViewModel.ReadingTime(new string('a', 100)) < SpeechBubbleViewModel.ReadingTime(new string('a', 200)));
        }

        [Test]
        public async Task ANewMessageOfTheSameLength_RestartsTheWait()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            context.Provider.Reply.Add("Hello.");
            var waits = 0;
            bubble.PropertyChanged += (_, e) => waits += e.PropertyName == nameof(SpeechBubbleViewModel.HideAfter) && bubble.HideAfter is not null ? 1 : 0;

            context.Chat.Draft = "Hi";
            await context.Chat.SendAsync();
            context.Chat.Draft = "Hi";
            await context.Chat.SendAsync();

            Assert.Equal(2, waits);
        }

        [Test]
        public async Task WhileTheTypingBubbleIsOpen_AFinishedReplyStays_AndGoesWhenItIsPutAway()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat) { KeepOpen = true };
            context.Provider.Reply.Add("Hello.");
            context.Chat.Draft = "Hi";

            await context.Chat.SendAsync();

            Assert.True(bubble.IsVisible);
            Assert.Equal("Hello.", bubble.Text);
            Assert.Null(bubble.HideAfter);

            // Clicking the rider puts the typing bubble away, and the rider's answer goes with it.
            bubble.KeepOpen = false;

            Assert.False(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);

            // Opening the typing bubble again does not bring the old answer back.
            bubble.KeepOpen = true;
            Assert.False(bubble.IsVisible);
        }

        [Test]
        public async Task OpeningTheTypingBubble_WhileAnAnswerIsWaitingToFade_HoldsIt()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            context.Provider.Reply.Add("Hello.");
            context.Chat.Draft = "Hi";
            await context.Chat.SendAsync();
            Assert.NotNull(bubble.HideAfter);

            bubble.KeepOpen = true;

            Assert.True(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);
        }

        [Test]
        public async Task ClosingTheTypingBubbleMidReply_LetsTheReplyFinish_ThenFadeByItself()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat) { KeepOpen = true };
            context.Provider.Reply.AddRange(new[] { "one ", "two" });
            context.Provider.Gate = new TaskCompletionSource();
            context.Chat.Draft = "Go";
            var sending = context.Chat.SendAsync();
            await context.Provider.FirstPieceSent.Task.WaitAsync(Patience);

            bubble.KeepOpen = false;
            Assert.True(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);

            context.Provider.Gate.SetResult();
            await sending.WaitAsync(Patience);
            Assert.Equal("one two", bubble.Text);
            Assert.NotNull(bubble.HideAfter);
        }

        [Test]
        public void KeepOpen_WithNothingShowing_ShowsNothing()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);

            bubble.KeepOpen = true;
            bubble.KeepOpen = false;

            Assert.False(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);
        }

        [Test]
        public async Task DuringATrick_TheBubbleStepsAside_AndComesBackAfterwardsWithItsFullReadingTime()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            context.Provider.Reply.Add("Sure, watch this!");
            context.Chat.Draft = "Do a backflip";
            await context.Chat.SendAsync();
            Assert.True(bubble.IsVisible);

            bubble.Performing = true;

            Assert.False(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);

            bubble.Performing = false;

            Assert.True(bubble.IsVisible);
            Assert.Equal("Sure, watch this!", bubble.Text);
            Assert.Equal(SpeechBubbleViewModel.ReadingTime("Sure, watch this!"), bubble.HideAfter);
        }

        [Test]
        public async Task AReplyThatArrivesDuringATrick_IsNotShownUntilTheTrickIsOver()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            bubble.Performing = true;
            context.Provider.Reply.AddRange(new[] { "Hold ", "tight!" });
            context.Chat.Draft = "Hi";
            var shown = false;
            bubble.PropertyChanged += (_, _) => shown |= bubble.IsVisible;

            await context.Chat.SendAsync();

            Assert.False(shown);
            Assert.Null(bubble.HideAfter);

            bubble.Performing = false;

            Assert.True(bubble.IsVisible);
            Assert.Equal("Hold tight!", bubble.Text);
        }

        [Test]
        public void ATrickWithNothingToSay_BringsNoBubbleBack()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);

            bubble.Performing = true;
            bubble.Performing = false;

            Assert.False(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);
        }

        [Test]
        public async Task ABubbleDismissedBeforeATrick_StaysAwayAfterIt()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            context.Provider.Reply.Add("Hello.");
            context.Chat.Draft = "Hi";
            await context.Chat.SendAsync();
            bubble.Dismiss();

            bubble.Performing = true;
            bubble.Performing = false;

            Assert.False(bubble.IsVisible);
        }

        [Test]
        public async Task WhileTheUserIsChattingInBubbles_TheReplyComesBackAfterATrick_AndStays()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat) { KeepOpen = true };
            context.Provider.Reply.Add("Hello.");
            context.Chat.Draft = "Hi";
            await context.Chat.SendAsync();

            bubble.Performing = true;
            bubble.Performing = false;

            Assert.True(bubble.IsVisible);
            Assert.Null(bubble.HideAfter);
        }

        [Test]
        public async Task WithTheConversationWindowOpen_ATrickBringsNoBubble()
        {
            using var context = new ConversationViewModelTests.Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat) { ConversationOpen = true };
            context.Provider.Reply.Add("Hello.");
            context.Chat.Draft = "Hi";
            await context.Chat.SendAsync();

            bubble.Performing = true;
            bubble.Performing = false;

            Assert.False(bubble.IsVisible);
        }
    }
}
