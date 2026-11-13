using DesktopRider.Animation;
using static DesktopRider.Animation.Tests.TestManifests;

namespace DesktopRider.Animation.Tests;

public class AnimationPlayerTests
{
    private static AnimationPlayer NewPlayer() => new(Create());

    private static List<string> Trace(AnimationPlayer player, int frames)
    {
        var seen = new List<string> { player.Current.Name };
        for (var i = 0; i < frames; i++)
        {
            player.Advance(Frames(1));
            if (seen[^1] != player.Current.Name)
            {
                seen.Add(player.Current.Name);
            }
        }

        return seen;
    }

    [Fact]
    public void StartsRestingOnTheFirstIdleFrame()
    {
        var player = NewPlayer();

        Assert.Equal("Idle", player.Current.Name);
        Assert.Equal(0, player.FrameIndex);
        Assert.True(player.IsResting);
    }

    [Fact]
    public void Loop_WrapsRoundToTheStart()
    {
        var player = NewPlayer();

        player.Advance(Frames(3.5));
        Assert.Equal(3, player.FrameIndex);
        player.Advance(Frames(1));
        Assert.Equal(0, player.FrameIndex);
        player.Advance(Frames(4 * 25 + 2));
        Assert.Equal(2, player.FrameIndex);
    }

    [Fact]
    public void Advance_ReportsOnlyRealFrameChanges()
    {
        var player = NewPlayer();
        var raised = 0;
        player.FrameChanged += (_, _) => raised++;

        Assert.False(player.Advance(Frames(0.4)));
        Assert.False(player.Advance(TimeSpan.Zero));
        Assert.False(player.Advance(TimeSpan.FromSeconds(-1)));
        Assert.Equal(0, raised);
        Assert.True(player.Advance(Frames(0.7)));
        Assert.Equal(1, raised);
        Assert.Equal(1, player.FrameIndex);
    }

    [Fact]
    public void OneShot_PlaysOnceThenReturnsToIdle()
    {
        var player = NewPlayer();

        Assert.True(player.Request("Wave"));
        Assert.Equal("Wave", player.Current.Name);
        Assert.Equal(0, player.FrameIndex);
        Assert.False(player.IsResting);
        player.Advance(Frames(4.5));
        Assert.Equal("Wave", player.Current.Name);
        Assert.Equal(4, player.FrameIndex);
        player.Advance(Frames(1));
        Assert.Equal("Idle", player.Current.Name);
        Assert.True(player.IsResting);
    }

    [Fact]
    public void OneShot_CarriesLeftoverTimeIntoTheNextAnimation()
    {
        var player = NewPlayer();
        player.Request("Wave");

        player.Advance(Frames(5 + 2.5));

        Assert.Equal("Idle", player.Current.Name);
        Assert.Equal(2, player.FrameIndex);
    }

    [Fact]
    public void OneShot_ReturnsToTheLoopThatWasPlaying()
    {
        var player = NewPlayer();
        player.Request("Talking");
        player.Request("Wave");

        Assert.Equal(["Wave", "Talking"], Trace(player, 8));
    }

    [Fact]
    public void RequestDuringAOneShot_WaitsForItToFinish()
    {
        var player = NewPlayer();
        player.Request("Wave");
        player.Advance(Frames(2));

        Assert.True(player.Request("Wheelie"));
        Assert.Equal("Wave", player.Current.Name);
        Assert.Equal(["Wave", "Wheelie", "Idle"], Trace(player, 12));
    }

    [Fact]
    public void LowerPriorityRequest_IsRefusedWhileAnImportantOneShotPlays()
    {
        var player = NewPlayer();
        player.Request("Wheelie", priority: 5);

        Assert.False(player.Request("Wave", priority: 1));
        Assert.Equal(["Wheelie", "Idle"], Trace(player, 10));
    }

    [Fact]
    public void WaitingRequest_IsOnlyReplacedByOneAtLeastAsImportant()
    {
        var player = NewPlayer();
        player.Request("Wave");
        Assert.True(player.Request("Wheelie", priority: 3));

        Assert.False(player.Request("Talking", priority: 1));
        Assert.True(player.Request("Talking", priority: 3));
        Assert.Equal(["Wave", "Talking"], Trace(player, 12));
    }

    [Fact]
    public void PriorityIsForgottenOnceTheOneShotEnds()
    {
        var player = NewPlayer();
        player.Request("Wheelie", priority: 9);
        player.Advance(Frames(7));

        Assert.True(player.Request("Wave", priority: 0));
        Assert.Equal("Wave", player.Current.Name);
    }

    [Fact]
    public void LoopWithLeadIn_PlaysItFirst()
    {
        var player = NewPlayer();
        player.Request("Sleeping");

        Assert.Equal("SleepStart", player.Current.Name);
        Assert.Equal(["SleepStart", "Sleeping"], Trace(player, 12));
    }

    [Fact]
    public void LeavingALoop_PlaysItsWayOutBeforeAnythingElse()
    {
        var player = NewPlayer();
        player.Request("Sleeping");
        player.Advance(Frames(6));
        Assert.Equal("Sleeping", player.Current.Name);

        player.Request("Wave");

        Assert.Equal(["SleepEnd", "Wave", "Idle"], Trace(player, 12));
    }

    [Fact]
    public void LeavingALoopForIdle_PlaysItsWayOut()
    {
        var player = NewPlayer();
        player.Request("Sleeping");
        player.Advance(Frames(6));

        player.Request("Idle");

        Assert.Equal(["SleepEnd", "Idle"], Trace(player, 6));
    }

    [Fact]
    public void RequestDuringALeadIn_StillComesBackOutOfTheLoopPose()
    {
        var player = NewPlayer();
        player.Request("Sleeping");
        player.Advance(Frames(1));
        Assert.Equal("SleepStart", player.Current.Name);

        player.Request("Wave");

        Assert.Equal(["SleepStart", "SleepEnd", "Wave", "Idle"], Trace(player, 14));
    }

    [Fact]
    public void RequestDuringTheWayOut_PlaysStraightAfterIt()
    {
        var player = NewPlayer();
        player.Request("Sleeping");
        player.Advance(Frames(6));
        player.Request("Idle");
        Assert.Equal("SleepEnd", player.Current.Name);

        player.Request("Wave");

        Assert.Equal(["SleepEnd", "Wave", "Idle"], Trace(player, 12));
    }

    [Fact]
    public void RequestingTheLoopAlreadyPlaying_DoesNotRestartIt()
    {
        var player = NewPlayer();
        player.Advance(Frames(2));

        Assert.True(player.Request("Idle"));
        Assert.Equal(2, player.FrameIndex);
    }

    [Fact]
    public void Speed_ScalesPlayback()
    {
        var player = NewPlayer();
        player.Speed = 2;
        player.Advance(Frames(1));
        Assert.Equal(2, player.FrameIndex);

        player.Speed = 0.5;
        player.Advance(Frames(2));
        Assert.Equal(3, player.FrameIndex);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Speed_RejectsValuesThatWouldBreakPlayback(double speed)
    {
        var player = NewPlayer();

        Assert.Throws<ArgumentOutOfRangeException>(() => player.Speed = speed);
    }

    [Fact]
    public void Request_RejectsUnknownAnimations()
    {
        Assert.Throws<KeyNotFoundException>(() => NewPlayer().Request("Backflip"));
    }

    [Fact]
    public void AVeryLongPause_DoesNotHangOrSkipTheReturnToIdle()
    {
        var player = NewPlayer();
        player.Request("Wave");

        player.Advance(TimeSpan.FromHours(3));

        Assert.Equal("Idle", player.Current.Name);
        Assert.InRange(player.FrameIndex, 0, 3);
    }
}
