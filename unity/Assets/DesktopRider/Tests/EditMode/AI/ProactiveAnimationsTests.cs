#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Context = DesktopRider.AI.Tests.ConversationViewModelTests.Context;
using DesktopRider.Core.Configuration;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    /// <summary>The rider doing a trick or a gesture by itself now and then, while it is allowed to start conversations.</summary>
    public class ProactiveAnimationsTests
    {
        [Test]
        public void ByDefault_ItSetsNoTimer_AndTheRiderDoesNothingByItself()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            using var animations = new ProactiveAnimations(comments, context.Settings, context.Clock, new Random(1));
            var done = Done(animations);

            animations.Start();
            context.Clock.Advance(TimeSpan.FromDays(2));

            Assert.False(animations.Active);
            Assert.Null(animations.NextDue);
            Assert.Equal(0, context.Clock.Waiting);
            Assert.Empty(done);
        }

        [Test]
        public void WhileTheRiderMayStartConversations_ItDoesSomethingNowAndThen_WithoutContactingTheAi()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            using var animations = Started(context, comments);
            var done = Done(animations);
            var (shortest, longest) = ProactiveAnimations.WaitFor(AnimationFrequency.Normal);

            context.Clock.Advance(shortest - TimeSpan.FromSeconds(1));
            Assert.Empty(done);

            context.Clock.Advance(longest - shortest + TimeSpan.FromSeconds(1));
            Assert.Single(done);
            Assert.Contains(done[0], RiderActions.All);

            for (var i = 0; i < 40; i++)
            {
                context.Clock.Advance(longest);
            }

            // Each wait is somewhere between the shortest and the longest, so forty of the longest see at least forty more.
            Assert.True(done.Count >= 41, done.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.All(done, action => Assert.Contains(action, RiderActions.All));
            Assert.All(done.Zip(done.Skip(1), (first, second) => (First: first, Second: second)), pair => Assert.NotEqual(pair.First, pair.Second));
            Assert.Contains(done, action => action.Kind == RiderActionKind.Trick);
            Assert.Empty(context.Provider.Requests);
            Assert.Empty(context.Chat.Lines);
        }

        [Test]
        public void TheWait_IsDifferentEachTime_AndStaysBetweenTheShortestAndLongest()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            using var animations = Started(context, comments);
            var (shortest, longest) = ProactiveAnimations.WaitFor(AnimationFrequency.Normal);
            var waits = new List<TimeSpan>();

            for (var i = 0; i < 20; i++)
            {
                var wait = animations.NextDue!.Value - context.Clock.Now;
                waits.Add(wait);
                context.Clock.Advance(wait);
            }

            Assert.All(waits, wait => Assert.InRange(wait, shortest, longest));
            Assert.True(waits.Distinct().Count() > 10);
        }
        [TestCase(AnimationFrequency.Rare, 8, 16)]
        [TestCase(AnimationFrequency.Normal, 3, 8)]
        [TestCase(AnimationFrequency.Frequent, 1, 3)]
        public void HowOften_FollowsTheUsersChoice(AnimationFrequency frequency, int shortest, int longest)
        {
            Assert.Equal((TimeSpan.FromMinutes(shortest), TimeSpan.FromMinutes(longest)), ProactiveAnimations.WaitFor(frequency));
        }

        [Test]
        public void TheRiderStaysStill_WhileMuted_InQuietHours_Hidden_OrAnswering()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            using var animations = Started(context, comments);
            var done = Done(animations);
            var longest = ProactiveAnimations.WaitFor(AnimationFrequency.Normal).Longest;

            comments.Mute();
            context.Clock.Advance(longest);
            Assert.Empty(done);
            comments.Unmute();

            context.Settings.Current.Appearance.CharacterVisible = false;
            context.Clock.Advance(longest);
            Assert.Empty(done);
            context.Settings.Current.Appearance.CharacterVisible = true;

            var behaviour = context.Settings.Current.Behaviour;
            var now = TimeOnly.FromDateTime(context.Clock.GetLocalNow().DateTime);
            behaviour.QuietHoursEnabled = true;
            behaviour.QuietHoursStart = now.AddHours(-1);
            behaviour.QuietHoursEnd = now.AddHours(1);
            context.Clock.Advance(longest);
            Assert.Empty(done);
            behaviour.QuietHoursEnabled = false;

            // It carries on afterwards: the timer was set again each time.
            context.Clock.Advance(longest);
            Assert.NotEmpty(done);
        }

        [Test]
        public async Task TheRiderStaysStill_WhileItIsAnswering()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            using var animations = Started(context, comments);
            var done = Done(animations);
            context.Provider.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Provider.Reply.AddRange(new[] { "Hello ", "there" });
            context.Chat.Draft = "Hi";

            var sending = context.Chat.SendAsync();
            await context.Provider.FirstPieceSent.Task;
            context.Clock.Advance(ProactiveAnimations.WaitFor(AnimationFrequency.Normal).Longest);
            Assert.True(context.Chat.IsBusy);
            Assert.Empty(done);

            context.Provider.Gate.SetResult();
            await sending;
        }

        [Test]
        public async Task SwitchingItOff_OrSwitchingSmallMovementsOff_TakesTheTimerAway()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            using var animations = Started(context, comments);
            var done = Done(animations);

            await context.Settings.UpdateAsync(s => s.Behaviour.IdleAnimations = false);
            Assert.Null(animations.NextDue);
            Assert.Equal(0, context.Clock.Waiting);

            await context.Settings.UpdateAsync(s => s.Behaviour.IdleAnimations = true);
            Assert.NotNull(animations.NextDue);

            await context.Settings.UpdateAsync(s => s.Behaviour.ProactiveComments = false);
            Assert.Null(animations.NextDue);
            context.Clock.Advance(TimeSpan.FromDays(1));
            Assert.Empty(done);
        }

        [Test]
        public async Task AnUnrelatedSettingsChange_DoesNotStartTheWaitAgain()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            using var animations = Started(context, comments);
            var due = animations.NextDue;

            context.Clock.Advance(TimeSpan.FromSeconds(30));
            await context.Settings.UpdateAsync(s => s.Appearance.Opacity = 0.5);

            Assert.Equal(due, animations.NextDue);
        }

        [Test]
        public void TheWork_IsHandedToTheInterfaceThread()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            context.Settings.Current.Behaviour.ProactiveComments = true;
            var handed = new List<Action>();
            using var animations = new ProactiveAnimations(comments, context.Settings, context.Clock, new Random(1), handed.Add);
            var done = Done(animations);
            animations.Start();

            context.Clock.Advance(ProactiveAnimations.WaitFor(AnimationFrequency.Normal).Longest);

            Assert.Empty(done);
            Assert.Single(handed)();
            Assert.Single(done);
        }

        [Test]
        public void AfterShutdown_NothingMoreHappens()
        {
            using var context = new Context();
            using var comments = new ProactiveComments(context.Chat, context.Settings, context.Clock);
            var animations = Started(context, comments);
            var done = Done(animations);

            animations.Dispose();
            animations.Dispose();
            context.Clock.Advance(TimeSpan.FromHours(5));

            Assert.Equal(0, context.Clock.Waiting);
            Assert.Empty(done);
        }

        private static ProactiveAnimations Started(Context context, ProactiveComments comments)
        {
            context.Settings.Current.Behaviour.ProactiveComments = true;
            var animations = new ProactiveAnimations(comments, context.Settings, context.Clock, new Random(1));
            animations.Start();
            return animations;
        }

        private static List<RiderAction> Done(ProactiveAnimations animations)
        {
            var done = new List<RiderAction>();
            animations.ActionRequested += (_, e) => done.Add(e.Action);
            return done;
        }
    }
}
