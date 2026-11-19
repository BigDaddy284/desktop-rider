using DesktopRider.Animation;

namespace DesktopRider.Animation.Tests;

public class RidingMotionTests
{
    private const string Riding = """
        {
          "frameWidth": 100, "frameHeight": 80, "framesPerSecond": 10, "pixelsPerMetre": 200, "rideSpeed": 3, "displayName": "Test rider",
          "animations": [
            { "name": "Idle", "loop": true, "frameCount": 4, "columns": 4, "right": "R/Idle.png", "left": "L/Idle.png" },
            { "name": "RideOff", "loop": false, "frameCount": 4, "columns": 4, "right": "R/RideOff.png", "left": "L/RideOff.png" },
            { "name": "RidingLoop", "loop": true, "frameCount": 4, "columns": 4, "right": "R/RidingLoop.png", "left": "L/RidingLoop.png", "enter": "RideOff", "exit": "RideOn" },
            { "name": "RideOn", "loop": false, "frameCount": 4, "columns": 4, "right": "R/RideOn.png", "left": "L/RideOn.png" }
          ]
        }
        """;

    [Theory]
    [InlineData("RideOff", true)]
    [InlineData("ridingloop", true)]
    [InlineData("RideOn", true)]
    [InlineData("Idle", false)]
    [InlineData("Wheelie", false)]
    public void Knows_which_animations_are_riding(string animation, bool riding) =>
        Assert.Equal(riding, RidingMotion.IsRiding(animation));

    [Fact]
    public void Speeds_up_while_riding_off_holds_speed_and_slows_down_when_stopping()
    {
        Assert.Equal(0, RidingMotion.SpeedShare("RideOff", 0));
        Assert.Equal(0.5, RidingMotion.SpeedShare("RideOff", 0.5));
        Assert.Equal(1, RidingMotion.SpeedShare("RidingLoop", 0.3));
        Assert.Equal(0.75, RidingMotion.SpeedShare("RideOn", 0.25));
        Assert.Equal(0, RidingMotion.SpeedShare("RideOn", 1));
        Assert.Equal(0, RidingMotion.SpeedShare("Idle", 0.5));
    }

    [Fact]
    public void Progress_outside_the_animation_is_kept_inside_it()
    {
        Assert.Equal(1, RidingMotion.SpeedShare("RideOff", 7));
        Assert.Equal(0, RidingMotion.SpeedShare("RideOff", -1));
        Assert.Equal(0, RidingMotion.SpeedShare("RideOff", double.NaN));
    }

    [Fact]
    public void Screen_speed_follows_the_characters_speed_its_size_and_the_users_choice()
    {
        var manifest = SpriteManifest.Parse(Riding);
        Assert.Equal("Test rider", manifest.DisplayName);
        // 3 m/s x 200 px/m x 0.5 on screen = 300 px/s
        Assert.Equal(300, RidingMotion.PixelsPerSecond(manifest, 0.5, 1.0), 6);
        Assert.Equal(600, RidingMotion.PixelsPerSecond(manifest, 0.5, 2.0), 6);
        Assert.Equal(75, RidingMotion.PixelsPerSecond(manifest, 0.5, 0.25), 6);
    }

    [Theory]
    [InlineData(5.0, 2.0)]
    [InlineData(0.01, 0.25)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    [InlineData(1.5, 1.5)]
    public void Keeps_the_users_speed_choice_in_range(double chosen, double used) =>
        Assert.Equal(used, RidingMotion.ClampSpeed(chosen));

    [Fact]
    public void A_character_that_gives_no_riding_speed_stays_still() =>
        Assert.Equal(0, RidingMotion.PixelsPerSecond(TestManifests.Create(), 0.5, 1.0));

    [Fact]
    public void A_negative_riding_speed_is_refused() =>
        Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse(Riding.Replace("\"rideSpeed\": 3", "\"rideSpeed\": -3")));
}
