using DesktopRider.AI;

namespace DesktopRider.AI.Tests;

// Changes which actions are on offer for everyone, so it runs on its own after the other tests.
[CollectionDefinition(nameof(CharacterActions), DisableParallelization = true)]
public sealed class CharacterActions;

[Collection(nameof(CharacterActions))]
public sealed class RiderActionsCharacterTests : IDisposable
{
    private static readonly string[] SidecarAnimations =
        ["Idle", "IdleVariation", "Revving", "Thinking", "Talking", "Celebration", "SleepStart", "Sleeping", "SleepEnd", "RideOff", "RidingLoop", "RideOn",
         "Wheelie", "FlyingChair", "PowerSlide", "PassengerWave", "LostPassenger", "Rollers", "Double", "NoHander", "PassengerSuperman"];

    public void Dispose() => RiderActions.UseAnimations(null);

    [Fact]
    public void The_sidecar_crew_is_only_offered_what_it_can_do()
    {
        RiderActions.UseAnimations(SidecarAnimations);

        var tags = RiderActions.All.Select(a => a.Tag).ToList();
        Assert.Contains("double", tags);
        Assert.Contains("passenger-superman", tags);
        Assert.Contains("wheelie", tags);
        Assert.Contains("ride", tags);
        Assert.DoesNotContain("backflip", tags);
        Assert.DoesNotContain("superman", tags);
        Assert.DoesNotContain("look-left", tags);
        Assert.All(RiderActions.All, a => Assert.Contains(a.Animation, SidecarAnimations));
        Assert.DoesNotContain("[backflip]", RiderActions.Instructions, StringComparison.Ordinal);
        Assert.Contains("[double]", RiderActions.Instructions, StringComparison.Ordinal);
        Assert.DoesNotContain(RiderActions.Tricks, a => a.Kind != RiderActionKind.Trick);
    }

    [Fact]
    public void Asking_the_sidecar_crew_for_a_jump_or_a_superman_gets_their_own_version()
    {
        RiderActions.UseAnimations(SidecarAnimations);

        Assert.Equal("Double", Assert.Single(RiderActions.Named("go on, do a jump")).Animation);
        Assert.Equal("PassengerSuperman", Assert.Single(RiderActions.Named("can you do a superman")).Animation);
        Assert.Equal("Double", RiderActions.FromTag("[double]")?.Animation);
        Assert.Null(RiderActions.FromTag("[backflip]"));
    }

    [Fact]
    public void Harvey_keeps_every_action_with_his_real_animations()
    {
        var everything = RiderActions.All.Select(a => a.Tag).ToList();

        RiderActions.UseAnimations(AnimationsIn("Rider"));

        Assert.Equal(everything, RiderActions.All.Select(a => a.Tag));
    }

    [Fact]
    public void Every_sidecar_action_has_its_animation_in_the_real_sidecar_sprites()
    {
        var real = AnimationsIn("Sidecar41");

        RiderActions.UseAnimations(real);

        Assert.Equal(13, RiderActions.All.Count);
        Assert.All(RiderActions.All, a => Assert.Contains(a.Animation, real));
    }

    private static List<string> AnimationsIn(string character)
    {
        var folder = AppContext.BaseDirectory;
        while (folder is not null && !File.Exists(Path.Combine(folder, "Assets", "Sprites", character, "sprites.json")))
        {
            folder = Path.GetDirectoryName(folder);
        }

        Assert.NotNull(folder);
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "Assets", "Sprites", character, "sprites.json")));
        return [.. document.RootElement.GetProperty("animations").EnumerateArray().Select(a => a.GetProperty("name").GetString()!)];
    }

    [Fact]
    public void Putting_the_standard_rider_back_offers_everything_it_can_do_again()
    {
        RiderActions.UseAnimations(SidecarAnimations);
        RiderActions.UseAnimations(null);

        var tags = RiderActions.All.Select(a => a.Tag).ToList();
        Assert.Contains("backflip", tags);
        Assert.DoesNotContain("double", tags);
        Assert.Equal("Superman", Assert.Single(RiderActions.Named("can you do a superman")).Animation);
    }
}
