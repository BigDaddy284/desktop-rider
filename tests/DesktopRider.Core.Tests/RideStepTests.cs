using DesktopRider.Core.Placement;

namespace DesktopRider.Core.Tests;

public class RideStepTests
{
    private static readonly ScreenArea Left = new("LEFT", new PixelRect(0, 0, 1920, 1040), true);
    private static readonly ScreenArea Right = new("RIGHT", new PixelRect(1920, 0, 1920, 1040), false);

    [Fact]
    public void Rides_along_while_there_is_screen_ahead()
    {
        var (position, blocked) = PlacementCalculator.RideStep(new PixelRect(500, 800, 300, 200), 12, [Left]);

        Assert.Equal(new PixelPoint(512, 800), position);
        Assert.False(blocked);
    }

    [Fact]
    public void Stops_at_the_right_hand_edge()
    {
        var (position, blocked) = PlacementCalculator.RideStep(new PixelRect(1610, 800, 300, 200), 30, [Left]);

        Assert.Equal(new PixelPoint(1620, 800), position);
        Assert.True(blocked);
    }

    [Fact]
    public void Stops_at_the_left_hand_edge()
    {
        var (position, blocked) = PlacementCalculator.RideStep(new PixelRect(5, 800, 300, 200), -30, [Left]);

        Assert.Equal(new PixelPoint(0, 800), position);
        Assert.True(blocked);
    }

    [Fact]
    public void Rides_on_to_the_next_screen()
    {
        var (position, blocked) = PlacementCalculator.RideStep(new PixelRect(1610, 800, 300, 200), 30, [Left, Right]);

        Assert.Equal(new PixelPoint(1640, 800), position);
        Assert.False(blocked);
    }

    [Fact]
    public void Already_at_an_edge_it_stays_put_and_says_so()
    {
        var (position, blocked) = PlacementCalculator.RideStep(new PixelRect(3540, 800, 300, 200), 10, [Left, Right]);

        Assert.Equal(new PixelPoint(3540, 800), position);
        Assert.True(blocked);
    }

    [Fact]
    public void Not_moving_or_no_screens_changes_nothing()
    {
        Assert.Equal((new PixelPoint(10, 20), false), PlacementCalculator.RideStep(new PixelRect(10, 20, 30, 40), 0, [Left]));
        Assert.Equal((new PixelPoint(10, 20), false), PlacementCalculator.RideStep(new PixelRect(10, 20, 30, 40), 5, []));
    }
}
