namespace DesktopRider.AI.Tests;

public class RiderActionsTests
{
    [Theory]
    [InlineData("do a wheelie", "Wheelie")]
    [InlineData("Can you do a WHEELIE for me?", "Wheelie")]
    [InlineData("pop a wheely!", "Wheelie")]
    [InlineData("go on, rev it", "Revving")]
    [InlineData("Rev the engine please", "Revving")]
    [InlineData("time to celebrate", "Celebration")]
    [InlineData("you look tired, go to sleep", "Sleeping")]
    [InlineData("go for a ride", "RidingLoop")]
    [InlineData("look left", "LookLeft")]
    [InlineData("now look to your right", "LookRight")]
    [InlineData("hit the jump!", "Jump")]
    [InlineData("throw a whip", "Whip")]
    [InlineData("Do a backflip", "Backflip")]
    [InlineData("can you do a superman", "Superman")]
    [InlineData("superman seat grab please", "SupermanSeatGrab")]
    [InlineData("try a no-hander", "NoHander")]
    [InlineData("do a no footer", "NoFooter")]
    [InlineData("no hands and no feet this time", "Nothing")]
    [InlineData("heel clicker!", "HeelClicker")]
    [InlineData("do a can-can", "CanCan")]
    [InlineData("show me a nac-nac", "NacNac")]
    [InlineData("lazy boy next", "LazyBoy")]
    [InlineData("one hander", "OneHander")]
    [InlineData("scrub the jump", "Scrub")]
    [InlineData("indian air!", "IndianAir")]
    [InlineData("do a rock solid", "RockSolid")]
    [InlineData("go and hit the whoops", "Whoops")]
    [InlineData("do a stoppie", "Stoppie")]
    [InlineData("do a nose wheelie", "Stoppie")]
    public void FromRequest_RecognisesPlainRequests(string message, string animation)
    {
        Assert.Equal([animation], RiderActions.FromRequest(message).Select(action => action.Animation));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello there")]
    [InlineData("What is a wheelie?")]
    [InlineData("how do you rev an engine properly")]
    [InlineData("I bought some revolving doors")]
    [InlineData("did you sleep well")]
    [InlineData("that looks right to me")]
    [InlineData("whoops, sorry about that")]
    [InlineData("there is nothing to do today")]
    [InlineData("don't jump to conclusions")]
    [InlineData("what tricks do you know?")]
    public void FromRequest_IgnoresQuestionsAndLookalikes(string message)
    {
        Assert.Empty(RiderActions.FromRequest(message));
    }

    [Fact]
    public void FromRequest_CanFindMoreThanOne()
    {
        Assert.Equal(["Wheelie", "Revving"], RiderActions.FromRequest("rev it then do a wheelie").Select(action => action.Animation));
    }

    [Theory]
    [InlineData("[wheelie]", "Wheelie")]
    [InlineData("[ Wheelie ]", "Wheelie")]
    [InlineData("LOOK_LEFT", "LookLeft")]
    [InlineData("[look left]", "LookLeft")]
    [InlineData("[backflip]", "Backflip")]
    [InlineData("[seat grab]", "SupermanSeatGrab")]
    [InlineData("[NAC-NAC]", "NacNac")]
    public void FromTag_IsForgivingAboutHowTheTagIsWritten(string tag, string animation)
    {
        Assert.Equal(animation, RiderActions.FromTag(tag)?.Animation);
    }

    [Theory]
    [InlineData("[moonwalk]")]
    [InlineData("[]")]
    [InlineData("[1, 2]")]
    public void FromTag_IgnoresAnythingElse(string tag)
    {
        Assert.Null(RiderActions.FromTag(tag));
    }

    [Theory]
    [InlineData("do a trick")]
    [InlineData("Show me a trick!")]
    [InlineData("go on, show off")]
    public void FromRequest_PicksATrickWhenNoneIsNamed(string message)
    {
        var first = RiderActions.FromRequest(message, new Random(1));
        var again = RiderActions.FromRequest(message, new Random(1));

        Assert.Single(first);
        Assert.Contains(first[0], RiderActions.Tricks);
        Assert.Equal(first, again);
    }

    [Fact]
    public void FromRequest_ANamedTrickWinsOverAnyTrick()
    {
        Assert.Equal(["Backflip"], RiderActions.FromRequest("do a trick, a backflip", new Random(1)).Select(action => action.Animation));
    }

    [Fact]
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

    [Fact]
    public void EveryTagFitsTheFilter()
    {
        // The filter that hides tags from the user gives up on anything longer than this.
        Assert.All(RiderActions.All, action => Assert.True(action.Tag.Length + 2 <= 24, action.Tag));
    }

    [Fact]
    public void EveryActionHasItsOwnTag_AndIsDescribedToTheAi()
    {
        Assert.Equal(RiderActions.All.Count, RiderActions.All.Select(action => action.Tag).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(RiderActions.All, action => Assert.Contains($"[{action.Tag}] to {action.Description}", RiderActions.Instructions, StringComparison.Ordinal));
    }

    [Fact]
    public void Filter_RemovesATagSplitAcrossPieces()
    {
        var filter = new ActionTagFilter();

        var first = filter.Add("Watch [cele");
        var second = filter.Add("brate] this!");

        Assert.Equal("Watch ", first.Text);
        Assert.Empty(first.Actions);
        Assert.Equal(" this!", second.Text);
        Assert.Equal(["Celebration"], second.Actions.Select(action => action.Animation));
        Assert.Equal(string.Empty, filter.Finish());
    }

    [Fact]
    public void Filter_LeavesOtherBracketsAlone()
    {
        var filter = new ActionTagFilter();

        var text = filter.Add("a [note] b [[rev]] c [this is far too long to be a tag] d [new\nline] e").Text;

        Assert.Equal("a [note] b [] c [this is far too long to be a tag] d [new\nline] e", text);
    }

    [Fact]
    public void Filter_GivesBackAnUnfinishedBracketAtTheEnd()
    {
        var filter = new ActionTagFilter();

        Assert.Equal("see ", filter.Add("see [whee").Text);
        Assert.Equal("[whee", filter.Finish());
        Assert.Equal(string.Empty, filter.Finish());
    }
}
