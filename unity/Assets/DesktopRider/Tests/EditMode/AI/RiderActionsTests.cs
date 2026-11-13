#nullable enable

using System;
using System.Linq;
using NUnit.Framework;

namespace DesktopRider.AI.Tests
{
    public class RiderActionsTests
    {
        [TestCase("do a wheelie", "Wheelie")]
        [TestCase("Can you do a WHEELIE for me?", "Wheelie")]
        [TestCase("pop a wheely!", "Wheelie")]
        [TestCase("go on, rev it", "Revving")]
        [TestCase("Rev the engine please", "Revving")]
        [TestCase("time to celebrate", "Celebration")]
        [TestCase("you look tired, go to sleep", "Sleeping")]
        [TestCase("go for a ride", "RidingLoop")]
        [TestCase("look left", "LookLeft")]
        [TestCase("now look to your right", "LookRight")]
        [TestCase("hit the jump!", "Jump")]
        [TestCase("throw a whip", "Whip")]
        [TestCase("Do a backflip", "Backflip")]
        [TestCase("can you do a superman", "Superman")]
        [TestCase("superman seat grab please", "SupermanSeatGrab")]
        [TestCase("try a no-hander", "NoHander")]
        [TestCase("do a no footer", "NoFooter")]
        [TestCase("no hands and no feet this time", "Nothing")]
        [TestCase("heel clicker!", "HeelClicker")]
        [TestCase("do a can-can", "CanCan")]
        [TestCase("show me a nac-nac", "NacNac")]
        [TestCase("lazy boy next", "LazyBoy")]
        [TestCase("one hander", "OneHander")]
        [TestCase("scrub the jump", "Scrub")]
        [TestCase("indian air!", "IndianAir")]
        [TestCase("do a rock solid", "RockSolid")]
        [TestCase("go and hit the whoops", "Whoops")]
        [TestCase("do a stoppie", "Stoppie")]
        [TestCase("do a nose wheelie", "Stoppie")]
        public void FromRequest_RecognisesPlainRequests(string message, string animation)
        {
            Assert.Equal(new[] { animation }, RiderActions.FromRequest(message).Select(action => action.Animation));
        }
        [TestCase("")]
        [TestCase("hello there")]
        [TestCase("What is a wheelie?")]
        [TestCase("how do you rev an engine properly")]
        [TestCase("I bought some revolving doors")]
        [TestCase("did you sleep well")]
        [TestCase("that looks right to me")]
        [TestCase("whoops, sorry about that")]
        [TestCase("there is nothing to do today")]
        [TestCase("don't jump to conclusions")]
        [TestCase("what tricks do you know?")]
        public void FromRequest_IgnoresQuestionsAndLookalikes(string message)
        {
            Assert.Empty(RiderActions.FromRequest(message));
        }

        [Test]
        public void FromRequest_CanFindMoreThanOne()
        {
            Assert.Equal(new[] { "Wheelie", "Revving" }, RiderActions.FromRequest("rev it then do a wheelie").Select(action => action.Animation));
        }
        [TestCase("[wheelie]", "Wheelie")]
        [TestCase("[ Wheelie ]", "Wheelie")]
        [TestCase("LOOK_LEFT", "LookLeft")]
        [TestCase("[look left]", "LookLeft")]
        [TestCase("[backflip]", "Backflip")]
        [TestCase("[seat grab]", "SupermanSeatGrab")]
        [TestCase("[NAC-NAC]", "NacNac")]
        public void FromTag_IsForgivingAboutHowTheTagIsWritten(string tag, string animation)
        {
            Assert.Equal(animation, RiderActions.FromTag(tag)?.Animation);
        }
        [TestCase("[moonwalk]")]
        [TestCase("[]")]
        [TestCase("[1, 2]")]
        public void FromTag_IgnoresAnythingElse(string tag)
        {
            Assert.Null(RiderActions.FromTag(tag));
        }
        [TestCase("do a trick")]
        [TestCase("Show me a trick!")]
        [TestCase("go on, show off")]
        public void FromRequest_PicksATrickWhenNoneIsNamed(string message)
        {
            var first = RiderActions.FromRequest(message, new Random(1));
            var again = RiderActions.FromRequest(message, new Random(1));

            Assert.Single(first);
            Assert.Contains(first[0], RiderActions.Tricks);
            Assert.Equal(first, again);
        }

        [Test]
        public void FromRequest_ANamedTrickWinsOverAnyTrick()
        {
            Assert.Equal(new[] { "Backflip" }, RiderActions.FromRequest("do a trick, a backflip", new Random(1)).Select(action => action.Animation));
        }

        [Test]
        public void Tricks_AreTheJumpsAndStunts_NotTheEverydayActions()
        {
            var names = RiderActions.Tricks.Select(action => action.Animation).ToList();

            Assert.Equal(19, names.Count);
            Assert.Contains("Wheelie", names);
            Assert.Contains("Backflip", names);
            Assert.Contains("Whoops", names);
            Assert.DoesNotContain("Sleeping", names);
            Assert.DoesNotContain("LookLeft", names);
        }
        [TestCase("do some tricks")]
        [TestCase("Can you do some tricks for me?")]
        [TestCase("show me a few of your best tricks")]
        [TestCase("do a couple of stunts")]
        [TestCase("more tricks!")]
        [TestCase("go on, put on a show")]
        [TestCase("I want to see some tricks")]
        [TestCase("show me what ya got")]
        public void FromRequest_PicksAFewDifferentTricks_WhenAskedForSome(string message)
        {
            Assert.Equal(TrickRequest.Several, RiderActions.TricksWanted(message));
            for (var seed = 0; seed < 50; seed++)
            {
                var picked = RiderActions.FromRequest(message, new Random(seed));

                Assert.InRange(picked.Count, RiderActions.FewestInARow, RiderActions.MostInARow);
                Assert.Equal(picked.Count, picked.Distinct().Count());
                Assert.All(picked, action => Assert.Equal(RiderActionKind.Trick, action.Kind));
            }
        }
        [TestCase("what tricks do you know?")]
        [TestCase("do you know any tricks?")]
        [TestCase("I learned some tricks in Excel today")]
        [TestCase("that was a trick question")]
        [TestCase("hello there")]
        public void TricksWanted_IgnoresTalkAboutTricks(string message)
        {
            Assert.Equal(TrickRequest.None, RiderActions.TricksWanted(message));
            Assert.Empty(RiderActions.FromRequest(message));
        }

        [Test]
        public void RandomTricks_AreOnlyEverTricks_AndLeaveOutTheOnesAlreadyDone()
        {
            var done = RiderActions.Tricks.Take(17).ToList();

            var picked = RiderActions.RandomTricks(5, new Random(3), done);

            Assert.Equal(RiderActions.Tricks.Skip(17).OrderBy(action => action.Tag), picked.OrderBy(action => action.Tag));
            Assert.Empty(RiderActions.RandomTricks(0, new Random(3)));
            Assert.Empty(RiderActions.RandomTricks(-2, new Random(3)));
        }

        [Test]
        public void EveryActionHasAKind_AndThinkingAndTalkingAreNotActionsAtAll()
        {
            Assert.Equal(new[] { "Revving", "RidingLoop" }, RiderActions.All.Where(action => action.Kind == RiderActionKind.Bike).Select(action => action.Animation));
            Assert.Equal(
                new[] { "Celebration", "Sleeping", "LookLeft", "LookRight" },
                RiderActions.All.Where(action => action.Kind == RiderActionKind.Gesture).Select(action => action.Animation));
            Assert.Equal(RiderActions.Tricks, RiderActions.All.Where(action => action.Kind == RiderActionKind.Trick));
            Assert.DoesNotContain(RiderActions.All, action => action.Animation is "Thinking" or "Talking" or "Idle" or "IdleVariation");
        }
        [TestCase("Ok, how about a no hander!", new[] { "NoHander" })]
        [TestCase("First a backflip, then a whip, and a no-hander to finish.", new[] { "Backflip", "Whip", "NoHander" })]
        [TestCase("A nose wheelie coming up", new[] { "Stoppie" })]
        [TestCase("Let me rev it and have a nap first", new string[0])]
        [TestCase("Sure thing!", new string[0])]
        public void Mentioned_FindsTheTricksTheRiderNamed_InTheOrderSaid_AndOnlyTricks(string reply, string[] animations)
        {
            Assert.Equal(animations, RiderActions.Mentioned(reply).Select(action => action.Animation));
        }
        [TestCase("[tricks]")]
        [TestCase("[ Tricks ]")]
        [TestCase("[stunts]")]
        public void FromTag_UnderstandsAFewTricksOfAnyKind(string tag)
        {
            Assert.Same(RiderActions.SomeTricks, RiderActions.FromTag(tag));
        }
        [TestCase("[trick]")]
        [TestCase("[stunt]")]
        public void FromTag_UnderstandsOneTrickOfAnyKind(string tag)
        {
            Assert.Same(RiderActions.AnyTrick, RiderActions.FromTag(tag));
        }

        [Test]
        public void TheAiIsToldWhichThingsAreTricks_AndThatItOnlyMovesWithATag()
        {
            var text = RiderActions.Instructions;
            var tricks = text.IndexOf("Tricks, jumps and stunts:", StringComparison.Ordinal);
            var bike = text.IndexOf("Ordinary things on the bike, which are not tricks:", StringComparison.Ordinal);
            var gestures = text.IndexOf("Gestures, which are not tricks:", StringComparison.Ordinal);

            Assert.True(tricks >= 0 && bike > tricks && gestures > bike);
            Assert.All(RiderActions.All, action =>
            {
                var at = text.IndexOf($"[{action.Tag}] to ", StringComparison.Ordinal);
                var expected = at < bike ? RiderActionKind.Trick : at < gestures ? RiderActionKind.Bike : RiderActionKind.Gesture;
                Assert.Equal(expected, action.Kind);
            });
            Assert.Contains("[tricks] to do a few different tricks in a row", text, StringComparison.Ordinal);
            Assert.Contains("You only move when you write a tag", text, StringComparison.Ordinal);
            Assert.Contains("thinking and talking movements happen by themselves and are not tricks", text, StringComparison.Ordinal);
        }

        [Test]
        public void Filter_HandsBackSeveralTagsInARow()
        {
            var filter = new ActionTagFilter();

            var (text, actions) = filter.Add("[backflip] [whip][tricks] Watch this!");

            Assert.Equal(new[] { "backflip", "whip", "tricks" }, actions.Select(action => action.Tag));
            Assert.Equal("  Watch this!", text);
        }
        [TestCase("Okay! Fancy a [backflip]?", "Okay! Fancy a backflip?")]
        [TestCase("Let's go for a [tricks]!", "Let's go for a few tricks!")]
        [TestCase("How about a [seat-grab], then a [trick]?", "How about a seat grab, then a trick?")]
        [TestCase("First, [whip] and away", "First, whip and away")]
        [TestCase("[backflip] Sure, watch this!", " Sure, watch this!")]
        [TestCase("Here it comes! [wheelie]", "Here it comes! ")]
        [TestCase("Time to [sleep] now", "Time to  now")]
        public void Filter_PutsATricksNameWhereItsTagWas_WhenTheTagIsInTheMiddleOfASentence(string reply, string shown)
        {
            var filter = new ActionTagFilter();

            Assert.Equal(shown, filter.Add(reply).Text);
            Assert.True(filter.SeenTag);
        }

        [Test]
        public void Filter_KnowsATagIsMidSentence_EvenWhenTheWordsCameInAnEarlierPiece()
        {
            var filter = new ActionTagFilter();

            Assert.Equal("Fancy a ", filter.Add("Fancy a [no-").Text);
            Assert.False(filter.SeenTag);
            Assert.Equal("no hander?", filter.Add("hander]?").Text);
        }

        [Test]
        public void TheAiIsToldToKeepTagsAtTheStart_AndToAnswerARequestInAFewEasyWords()
        {
            Assert.Contains("Tags go only at the very start of your reply", RiderActions.Instructions, StringComparison.Ordinal);
            Assert.Contains("\"Sure, watch this!\"", RiderActions.Instructions, StringComparison.Ordinal);
        }

        [Test]
        public void EveryTagFitsTheFilter()
        {
            // The filter that hides tags from the user gives up on anything longer than this.
            Assert.All(RiderActions.All, action => Assert.True(action.Tag.Length + 2 <= 24, action.Tag));
        }

        [Test]
        public void EveryActionHasItsOwnTag_AndIsDescribedToTheAi()
        {
            Assert.Equal(RiderActions.All.Count, RiderActions.All.Select(action => action.Tag).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(RiderActions.All, action => Assert.Contains($"[{action.Tag}] to {action.Description}", RiderActions.Instructions, StringComparison.Ordinal));
        }

        [Test]
        public void Filter_RemovesATagSplitAcrossPieces()
        {
            var filter = new ActionTagFilter();

            var first = filter.Add("Watch [cele");
            var second = filter.Add("brate] this!");

            Assert.Equal("Watch ", first.Text);
            Assert.Empty(first.Actions);
            Assert.Equal(" this!", second.Text);
            Assert.Equal(new[] { "Celebration" }, second.Actions.Select(action => action.Animation));
            Assert.Equal(string.Empty, filter.Finish());
        }

        [Test]
        public void Filter_LeavesOtherBracketsAlone()
        {
            var filter = new ActionTagFilter();

            var text = filter.Add("a [note] b [[rev]] c [this is far too long to be a tag] d [new\nline] e").Text;

            Assert.Equal("a [note] b [] c [this is far too long to be a tag] d [new\nline] e", text);
        }

        [Test]
        public void Filter_GivesBackAnUnfinishedBracketAtTheEnd()
        {
            var filter = new ActionTagFilter();

            Assert.Equal("see ", filter.Add("see [whee").Text);
            Assert.Equal("[whee", filter.Finish());
            Assert.Equal(string.Empty, filter.Finish());
        }
    }
}
