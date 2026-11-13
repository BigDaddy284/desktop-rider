#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Context = DesktopRider.AI.Tests.ConversationViewModelTests.Context;
using DesktopRider.Core.Configuration;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    /// <summary>The rider starting a conversation itself, which it only does when the user has switched that on.</summary>
    public class ProactiveCommentsTests
    {
        private static readonly TimeSpan HalfAnHour = TimeSpan.FromMinutes(30);
        [TestCase("23:30", true)]
        [TestCase("22:00", true)]
        [TestCase("03:00", true)]
        [TestCase("07:59", true)]
        [TestCase("08:00", false)]
        [TestCase("12:00", false)]
        [TestCase("21:59", false)]
        public void QuietHours_ThatRunPastMidnight_CoverTheNight(string time, bool quiet)
        {
            Assert.Equal(quiet, QuietHours.Cover(TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture), new TimeOnly(22, 0), new TimeOnly(8, 0)));
        }
        [TestCase("12:59", false)]
        [TestCase("13:00", true)]
        [TestCase("13:59", true)]
        [TestCase("14:00", false)]
        public void QuietHours_WithinOneDay_CoverOnlyThoseHours(string time, bool quiet)
        {
            Assert.Equal(quiet, QuietHours.Cover(TimeOnly.Parse(time, System.Globalization.CultureInfo.InvariantCulture), new TimeOnly(13, 0), new TimeOnly(14, 0)));
        }

        [Test]
        public void QuietHours_WithTheSameStartAndEnd_CoverNothing()
        {
            Assert.False(QuietHours.Cover(new TimeOnly(9, 0), new TimeOnly(9, 0), new TimeOnly(9, 0)));
            Assert.False(QuietHours.Cover(new TimeOnly(15, 0), new TimeOnly(9, 0), new TimeOnly(9, 0)));
        }

        [Test]
        public void ByDefault_ItIsOff_SetsNoTimer_AndNeverContactsTheAi()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            context.Provider.Reply.Add("Nice day for a ride!");

            comments.Start();
            context.Clock.Advance(TimeSpan.FromDays(2));

            Assert.False(new AppSettings().Behaviour.ProactiveComments);
            Assert.False(comments.Enabled);
            Assert.Null(comments.NextDue);
            Assert.Equal(0, context.Clock.Waiting);
            Assert.Empty(context.Provider.Requests);
            Assert.Equal(0, context.Provider.ListCalls);
            Assert.Empty(context.Chat.Lines);
            Assert.Null(comments.MuteChoiceText);
        }

        [Test]
        public void WhenSwitchedOn_TheRiderSpeaksAfterTheChosenTime_AndNotBefore()
        {
            using var context = new Context();
            using var comments = Started(context);
            context.Provider.Reply.AddRange(new[] { "Nice day ", "for a ride!" });

            context.Clock.Advance(HalfAnHour - TimeSpan.FromSeconds(1));
            Assert.Empty(context.Provider.Requests);

            context.Clock.Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(new[] { (Speaker.Rider, "Nice day for a ride!") }, context.Chat.Lines.Select(line => (line.Speaker, line.Text)));
            Assert.Equal("Nice day for a ride!", context.Chat.CurrentReply);
            Assert.Equal(ConversationState.Idle, context.Chat.State);
            var request = Assert.Single(context.Provider.Requests);
            Assert.Equal(RiderPersonality.OwnCommentRequest, request.Messages[^1].Content);

            // The request for a remark is never shown and never kept; the remark itself is.
            Assert.DoesNotContain(context.Chat.Lines, line => line.Text == RiderPersonality.OwnCommentRequest);
            Assert.Equal(new[] { "Nice day for a ride!" }, context.Store.Saved.Select(line => line.Text));
        }

        [Test]
        public void ARemark_ShowsNoThinking_OnlyTalking()
        {
            using var context = new Context();
            using var comments = Started(context);
            context.Provider.Reply.Add("Braaap!");
            var states = new List<ConversationState>();
            context.Chat.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ConversationViewModel.State))
                {
                    states.Add(context.Chat.State);
                }
            };

            context.Clock.Advance(HalfAnHour);

            Assert.Equal(new[] { ConversationState.Talking, ConversationState.Idle }, states);
        }

        [Test]
        public void ARemark_AppearsInTheSpeechBubble_AndThenGoesAwayByItself()
        {
            using var context = new Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            using var comments = Started(context);
            context.Provider.Reply.Add("Braaap!");

            context.Clock.Advance(HalfAnHour);

            Assert.True(bubble.IsVisible);
            Assert.Equal("Braaap!", bubble.Text);
            Assert.NotNull(bubble.HideAfter);
        }

        [Test]
        public void AfterThreeRemarksWithNoAnswer_TheRiderStops_UntilTheUserSpeaks()
        {
            using var context = new Context();
            using var comments = Started(context);
            context.Provider.Reply.Add("Braaap!");

            context.Clock.Advance(TimeSpan.FromHours(12));

            Assert.Equal(ProactiveComments.MaxUnanswered, context.Provider.Requests.Count);
            Assert.Null(comments.NextDue);
            Assert.Equal(0, context.Clock.Waiting);

            context.Chat.Draft = "Hello";
            context.Chat.SendCommand.Execute(null);
            context.Clock.Advance(HalfAnHour);

            // The user's message and its answer, then one more remark.
            Assert.Equal(ProactiveComments.MaxUnanswered + 2, context.Provider.Requests.Count);
            Assert.Equal(RiderPersonality.OwnCommentRequest, context.Provider.Requests[^1].Messages[^1].Content);
        }

        [Test]
        public void WhenTheUserSpeaks_TheWaitStartsAgain()
        {
            using var context = new Context();
            using var comments = Started(context);
            context.Provider.Reply.Add("Hi");

            context.Clock.Advance(TimeSpan.FromMinutes(20));
            context.Chat.Draft = "Hello";
            context.Chat.SendCommand.Execute(null);
            context.Clock.Advance(TimeSpan.FromMinutes(20));

            Assert.Single(context.Provider.Requests);
            Assert.Equal(context.Clock.Now.AddMinutes(10), comments.NextDue);

            context.Clock.Advance(TimeSpan.FromMinutes(10));
            Assert.Equal(2, context.Provider.Requests.Count);
        }

        [Test]
        public void WhileMuted_TheRiderSaysNothing_AndSpeaksAgainAfterTheHour()
        {
            using var context = new Context();
            using var comments = Started(context);
            context.Provider.Reply.Add("Braaap!");

            comments.Mute();
            Assert.True(comments.IsMuted);
            Assert.Equal(ProactiveComments.UnmuteText, comments.MuteChoiceText);
            context.Clock.Advance(TimeSpan.FromMinutes(59));
            Assert.Empty(context.Provider.Requests);

            // The hour is up exactly as the rider's next turn comes round.
            context.Clock.Advance(TimeSpan.FromMinutes(1));
            Assert.False(comments.IsMuted);
            Assert.Single(context.Provider.Requests);
        }

        [Test]
        public void Unmuting_LetsTheRiderSpeakAtTheNextTurn()
        {
            using var context = new Context();
            using var comments = Started(context);
            context.Provider.Reply.Add("Braaap!");

            comments.ToggleMute();
            comments.ToggleMute();
            context.Clock.Advance(HalfAnHour);

            Assert.False(comments.IsMuted);
            Assert.Equal(ProactiveComments.MuteText, comments.MuteChoiceText);
            Assert.Single(context.Provider.Requests);
        }

        [Test]
        public void DuringQuietHours_TheRiderSaysNothing()
        {
            // The clock starts at midday. Quiet from 12:15 to 13:45 covers the turns at 12:30, 13:00 and 13:30.
            using var context = new Context();
            context.Settings.Current.Behaviour.QuietHoursEnabled = true;
            context.Settings.Current.Behaviour.QuietHoursStart = new TimeOnly(12, 15);
            context.Settings.Current.Behaviour.QuietHoursEnd = new TimeOnly(13, 45);
            using var comments = Started(context);
            context.Provider.Reply.Add("Braaap!");

            context.Clock.Advance(TimeSpan.FromMinutes(90));
            Assert.Empty(context.Provider.Requests);

            context.Clock.Advance(HalfAnHour);
            Assert.Single(context.Provider.Requests);
        }

        [Test]
        public void QuietHoursThatAreSwitchedOff_DoNotApply()
        {
            using var context = new Context();
            context.Settings.Current.Behaviour.QuietHoursEnabled = false;
            context.Settings.Current.Behaviour.QuietHoursStart = new TimeOnly(0, 0);
            context.Settings.Current.Behaviour.QuietHoursEnd = new TimeOnly(23, 59);
            using var comments = Started(context);
            context.Provider.Reply.Add("Braaap!");

            context.Clock.Advance(HalfAnHour);

            Assert.Single(context.Provider.Requests);
        }
        [TestCase(true, null)]
        [TestCase(false, "picked-model")]
        public void WithTheRiderHidden_OrNoModelChosen_NothingIsAsked(bool visible, string? model)
        {
            using var context = new Context(model);
            context.Settings.Current.Appearance.CharacterVisible = visible;
            using var comments = Started(context);
            context.Provider.Reply.Add("Braaap!");

            context.Clock.Advance(TimeSpan.FromHours(3));

            Assert.Empty(context.Provider.Requests);
            Assert.Empty(context.Chat.Lines);
            Assert.NotNull(comments.NextDue);
        }

        [Test]
        public void WhenTheAiCannotBeReached_NothingIsShown_AndTheRiderTriesAgainNextTime()
        {
            using var context = new Context();
            using var bubble = new SpeechBubbleViewModel(context.Chat);
            using var comments = Started(context);
            context.Provider.Problem = new AiProviderException(AiErrorKind.NotRunning, "Ollama is not running.");
            var notices = 0;
            context.Chat.NoticeAdded += (_, _) => notices++;

            context.Clock.Advance(TimeSpan.FromHours(3));

            Assert.Equal(6, context.Provider.Requests.Count);
            Assert.Empty(context.Chat.Lines);
            Assert.Equal(0, notices);
            Assert.False(bubble.IsVisible);
            Assert.Equal(ConversationViewModel.NotConnectedText, context.Chat.ConnectionText);
            Assert.NotNull(comments.NextDue);
        }

        [Test]
        public void AnEmptyRemark_ShowsNothing()
        {
            using var context = new Context();
            using var comments = Started(context);
            context.Provider.Reply.Add("   ");

            context.Clock.Advance(HalfAnHour);

            Assert.Single(context.Provider.Requests);
            Assert.Empty(context.Chat.Lines);
        }

        [Test]
        public async Task WhileTheRiderIsAnswering_ItDoesNotInterruptItself()
        {
            using var context = new Context();
            using var comments = Started(context);
            context.Provider.Reply.AddRange(new[] { "one ", "two" });
            context.Provider.Gate = new TaskCompletionSource();
            context.Chat.Draft = "Hello";
            var sending = context.Chat.SendAsync();
            await context.Provider.FirstPieceSent.Task.WaitAsync(TimeSpan.FromSeconds(5));

            context.Clock.Advance(HalfAnHour);

            Assert.Single(context.Provider.Requests);
            context.Provider.Gate.SetResult();
            await sending;
            Assert.Equal(2, context.Chat.Lines.Count);
        }

        [Test]
        public async Task AMessageFromTheUser_ComesBeforeARemarkThatHasNotStarted()
        {
            using var context = new Context();
            var slow = new SlowStartChat(context.Service);
            using var chat = new ConversationViewModel(slow, context.Settings);
            context.Provider.Reply.Add("An answer");

            var remark = chat.CommentAsync();
            Assert.False(chat.IsBusy);
            chat.Draft = "Hello";
            var sending = chat.SendAsync();
            slow.Release();
            await sending;

            Assert.False(await remark);
            Assert.Equal(new[] { (Speaker.User, "Hello"), (Speaker.Rider, "An answer") }, chat.Lines.Select(line => (line.Speaker, line.Text)));
        }

        [Test]
        public async Task Comment_IsRefusedWhileBusy_AndAfterClosing()
        {
            using var context = new Context();
            context.Provider.Reply.AddRange(new[] { "one ", "two" });
            context.Provider.Gate = new TaskCompletionSource();
            context.Chat.Draft = "Hello";
            var sending = context.Chat.SendAsync();
            await context.Provider.FirstPieceSent.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.False(await context.Chat.CommentAsync());

            context.Provider.Gate.SetResult();
            await sending;
            context.Chat.Dispose();
            Assert.False(await context.Chat.CommentAsync());
            Assert.Single(context.Provider.Requests);
        }

        [Test]
        public void SwitchingItOnAndOff_SetsAndRemovesTheTimer()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            comments.Start();
            context.Provider.Reply.Add("Braaap!");

            context.Settings.UpdateAsync(s => s.Behaviour.ProactiveComments = true);
            Assert.Equal(context.Clock.Now + HalfAnHour, comments.NextDue);
            Assert.Equal(1, context.Clock.Waiting);

            context.Settings.UpdateAsync(s => s.Behaviour.ProactiveComments = false);
            Assert.Null(comments.NextDue);
            Assert.Equal(0, context.Clock.Waiting);

            context.Clock.Advance(TimeSpan.FromHours(5));
            Assert.Empty(context.Provider.Requests);
        }

        [Test]
        public void ChangingHowOften_StartsTheWaitAgain_ButOtherSettingsDoNot()
        {
            using var context = new Context();
            using var comments = Started(context);
            var first = comments.NextDue;

            context.Clock.Advance(TimeSpan.FromMinutes(10));
            context.Settings.UpdateAsync(s => s.Appearance.Opacity = 0.5);
            Assert.Equal(first, comments.NextDue);

            context.Settings.UpdateAsync(s => s.Behaviour.ProactiveIntervalMinutes = 60);
            Assert.Equal(context.Clock.Now.AddMinutes(60), comments.NextDue);
        }

        [Test]
        public void TheIconMenu_OffersTheSwitch_AndTheMuteChoiceOnlyWhileItIsOn()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            comments.Start();

            var off = comments.BuildEntries();
            Assert.Equal(new[] { ProactiveComments.EnableText }, off.Select(entry => entry.Text));
            Assert.False(off[0].IsChecked);

            off[0].Command!.Execute(null);

            Assert.True(context.Settings.Current.Behaviour.ProactiveComments);
            var on = comments.BuildEntries();
            Assert.Equal(new[] { ProactiveComments.EnableText, ProactiveComments.MuteText }, on.Select(entry => entry.Text));
            Assert.True(on[0].IsChecked);

            on[1].Command!.Execute(null);

            Assert.True(comments.IsMuted);
            Assert.Equal(ProactiveComments.UnmuteText, comments.BuildEntries()[1].Text);
        }

        [Test]
        public void WhenTheChoiceCannotBeSaved_NothingBreaks()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            comments.Start();
            context.Settings.FailSaves = true;

            comments.BuildEntries()[0].Command!.Execute(null);

            Assert.False(comments.Enabled);
            Assert.Null(comments.NextDue);
        }

        [Test]
        public void RemarksAreMadeOnTheInterfaceThread_WhenOneIsGiven()
        {
            using var context = new Context();
            context.Settings.Current.Behaviour.ProactiveComments = true;
            var handedOver = new List<Action>();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock, onInterfaceThread: handedOver.Add);
            comments.Start();
            context.Provider.Reply.Add("Braaap!");

            context.Clock.Advance(HalfAnHour);

            // The timer only hands the work over; nothing is said until the interface thread runs it.
            Assert.Empty(context.Provider.Requests);
            Assert.Single(handedOver)();
            Assert.Single(context.Provider.Requests);
        }

        [Test]
        public void AfterClosing_NothingMoreHappens()
        {
            using var context = new Context();
            var comments = Started(context);
            context.Provider.Reply.Add("Braaap!");

            comments.Dispose();
            comments.Dispose();
            context.Clock.Advance(TimeSpan.FromHours(5));

            Assert.Equal(0, context.Clock.Waiting);
            Assert.Empty(context.Provider.Requests);
        }

        private static ProactiveComments Started(Context context)
        {
            context.Settings.Current.Behaviour.ProactiveComments = true;
            var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            comments.Start();
            return comments;
        }

        /// <summary>A chat service whose replies do not begin until the test says so.</summary>
        private sealed class SlowStartChat : IAiChatService
        {
            private readonly IAiChatService _inner;
            private readonly TaskCompletionSource _go = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public SlowStartChat(IAiChatService inner) => _inner = inner;

            public void Release() => _go.TrySetResult();

            public Task<AiConnectionReport> CheckConnectionAsync(CancellationToken cancellationToken = default) => _inner.CheckConnectionAsync(cancellationToken);

            public Task SelectModelAsync(string model, CancellationToken cancellationToken = default) => _inner.SelectModelAsync(model, cancellationToken);

            public async IAsyncEnumerable<string> StreamReplyAsync(
                IReadOnlyList<ChatMessage> conversation,
                [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await _go.Task.WaitAsync(cancellationToken);
                await foreach (var piece in _inner.StreamReplyAsync(conversation, cancellationToken))
                {
                    yield return piece;
                }
            }
        }
    }
}
