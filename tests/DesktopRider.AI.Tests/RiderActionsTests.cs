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
    public void FromTag_IsForgivingAboutHowTheTagIsWritten(string tag, string animation)
    {
        Assert.Equal(animation, RiderActions.FromTag(tag)?.Animation);
    }

    [Theory]
    [InlineData("[backflip]")]
    [InlineData("[]")]
    [InlineData("[1, 2]")]
    public void FromTag_IgnoresAnythingElse(string tag)
    {
        Assert.Null(RiderActions.FromTag(tag));
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
