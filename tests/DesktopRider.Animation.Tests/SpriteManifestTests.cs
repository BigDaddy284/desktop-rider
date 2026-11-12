using DesktopRider.Animation;

namespace DesktopRider.Animation.Tests;

public class SpriteManifestTests
{
    [Fact]
    public void Parse_ReadsFrameSizeAndAnimations()
    {
        var manifest = TestManifests.Create();

        Assert.Equal(100, manifest.FrameWidth);
        Assert.Equal(80, manifest.FrameHeight);
        Assert.Equal(10, manifest.FramesPerSecond);
        Assert.Equal(76, manifest.GroundY);
        Assert.Equal(7, manifest.Animations.Count);
        Assert.Equal("Idle", manifest.Default.Name);
        Assert.True(manifest["Idle"].Loop);
        Assert.False(manifest["Wave"].Loop);
    }

    [Fact]
    public void Indexer_IgnoresCaseAndRejectsUnknownNames()
    {
        var manifest = TestManifests.Create();

        Assert.Same(manifest["Wave"], manifest["wave"]);
        Assert.True(manifest.TryGet("WHEELIE", out _));
        Assert.False(manifest.TryGet("Backflip", out _));
        Assert.Throws<KeyNotFoundException>(() => manifest["Backflip"]);
    }

    [Fact]
    public void SheetFor_PicksTheSheetForTheDirection()
    {
        var clip = TestManifests.Create()["Wave"];

        Assert.Equal("Right/Wave.png", clip.SheetFor(Facing.Right));
        Assert.Equal("Left/Wave.png", clip.SheetFor(Facing.Left));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(2, 200, 0)]
    [InlineData(3, 0, 80)]
    [InlineData(4, 100, 80)]
    public void FrameRectFor_WalksTheGridRowByRow(int frame, int x, int y)
    {
        var manifest = TestManifests.Create();

        Assert.Equal(new FrameRect(x, y, 100, 80), manifest.FrameRectFor(manifest["Wave"], frame));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void FrameRectFor_RejectsFramesOutsideTheAnimation(int frame)
    {
        var manifest = TestManifests.Create();

        Assert.Throws<ArgumentOutOfRangeException>(() => manifest.FrameRectFor(manifest["Wave"], frame));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{ "frameWidth": 0, "frameHeight": 80, "framesPerSecond": 10, "animations": [] }""")]
    [InlineData("""{ "frameWidth": 10, "frameHeight": 80, "framesPerSecond": 0, "animations": [ { "name": "Idle", "loop": true, "frameCount": 1, "columns": 1, "right": "a", "left": "b" } ] }""")]
    [InlineData("""{ "frameWidth": 10, "frameHeight": 80, "framesPerSecond": 10, "animations": [] }""")]
    [InlineData("""{ "frameWidth": 10, "frameHeight": 80, "framesPerSecond": 10, "animations": [ { "name": "Idle", "loop": true, "frameCount": 0, "columns": 1, "right": "a", "left": "b" } ] }""")]
    [InlineData("""{ "frameWidth": 10, "frameHeight": 80, "framesPerSecond": 10, "animations": [ { "name": "Idle", "loop": true, "frameCount": 1, "columns": 1, "right": "", "left": "b" } ] }""")]
    [InlineData("""{ "frameWidth": 10, "frameHeight": 80, "framesPerSecond": 10, "animations": [ { "name": "Idle", "loop": true, "frameCount": 1, "columns": 1, "right": "a", "left": "b" }, { "name": "idle", "loop": true, "frameCount": 1, "columns": 1, "right": "a", "left": "b" } ] }""")]
    [InlineData("""{ "frameWidth": 10, "frameHeight": 80, "framesPerSecond": 10, "animations": [ { "name": "Wave", "loop": false, "frameCount": 1, "columns": 1, "right": "a", "left": "b" } ] }""")]
    [InlineData("""{ "frameWidth": 10, "frameHeight": 80, "framesPerSecond": 10, "animations": [ { "name": "Idle", "loop": false, "frameCount": 1, "columns": 1, "right": "a", "left": "b" } ] }""")]
    [InlineData("""{ "frameWidth": 10, "frameHeight": 80, "framesPerSecond": 10, "animations": [ { "name": "Idle", "loop": true, "frameCount": 1, "columns": 1, "right": "a", "left": "b" }, { "name": "Nap", "loop": true, "frameCount": 1, "columns": 1, "right": "a", "left": "b", "enter": "Missing" } ] }""")]
    [InlineData("""{ "frameWidth": 10, "frameHeight": 80, "framesPerSecond": 10, "animations": [ { "name": "Idle", "loop": true, "frameCount": 1, "columns": 1, "right": "a", "left": "b" }, { "name": "Nap", "loop": true, "frameCount": 1, "columns": 1, "right": "a", "left": "b", "exit": "Idle" } ] }""")]
    public void Parse_ExplainsWhatIsWrongWithABadManifest(string json)
    {
        var error = Assert.Throws<InvalidDataException>(() => SpriteManifest.Parse(json));

        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    [Fact]
    public void RealRiderManifest_IsValidAndCoversEveryRequiredAnimation()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "RiderSprites", "sprites.json");
        if (!File.Exists(path))
        {
            return; // The sprites are not part of this checkout.
        }

        var manifest = SpriteManifest.Parse(File.ReadAllText(path));

        string[] required =
        [
            "Idle", "IdleVariation", "LookLeft", "LookRight", "Revving", "Thinking", "Talking",
            "Wheelie", "Celebration", "Sleeping", "RideOff", "RideOn", "RidingLoop",
        ];
        Assert.All(required, name => Assert.True(manifest.TryGet(name, out _), $"{name} is missing"));
        Assert.Equal("SleepStart", manifest["Sleeping"].Enter);
        Assert.Equal("SleepEnd", manifest["Sleeping"].Exit);
        Assert.Equal("RideOff", manifest["RidingLoop"].Enter);
        Assert.Equal("RideOn", manifest["RidingLoop"].Exit);
    }
}
