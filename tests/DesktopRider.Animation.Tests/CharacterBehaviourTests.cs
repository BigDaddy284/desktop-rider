using DesktopRider.Animation;

namespace DesktopRider.Animation.Tests;

public class CharacterBehaviourTests
{
    /// <summary>Ten frames a second. Thinking is a single five-frame gesture; Talking is a loop, as in the real sprites.</summary>
    private const string Json = """
        {
          "frameWidth": 100, "frameHeight": 80, "framesPerSecond": 10, "groundY": 76, "defaultAnimation": "Idle",
          "animations": [
            { "name": "Idle", "loop": true, "frameCount": 4, "columns": 4, "right": "Right/Idle.png", "left": "Left/Idle.png" },
            { "name": "Thinking", "loop": false, "frameCount": 5, "columns": 5, "right": "Right/Thinking.png", "left": "Left/Thinking.png" },
            { "name": "Talking", "loop": true, "frameCount": 3, "columns": 3, "right": "Right/Talking.png", "left": "Left/Talking.png" },
            { "name": "Wheelie", "loop": false, "frameCount": 6, "columns": 8, "right": "Right/Wheelie.png", "left": "Left/Wheelie.png" },
            { "name": "SleepStart", "loop": false, "frameCount": 2, "columns": 2, "right": "Right/SleepStart.png", "left": "Left/SleepStart.png" },
            { "name": "Sleeping", "loop": true, "frameCount": 4, "columns": 4, "right": "Right/Sleeping.png", "left": "Left/Sleeping.png", "enter": "SleepStart", "exit": "SleepEnd" },
            { "name": "SleepEnd", "loop": false, "frameCount": 2, "columns": 2, "right": "Right/SleepEnd.png", "left": "Left/SleepEnd.png" }
          ]
        }
        """;

    private static TimeSpan Frames(double count) => TimeSpan.FromSeconds(count / 10.0);

    private static (AnimationPlayer Player, CharacterBehaviour Behaviour) Create(string json = Json)
    {
        var manifest = SpriteManifest.Parse(json);
        var player = new AnimationPlayer(manifest);
        return (player, new CharacterBehaviour(player, manifest) { MinimumTalking = Frames(6) });
    }

    private static List<string> Trace(AnimationPlayer player, CharacterBehaviour behaviour, int frames)
    {
        var seen = new List<string> { player.Current.Name };
        for (var i = 0; i < frames; i++)
        {
            behaviour.Advance(Frames(1));
            if (seen[^1] != player.Current.Name)
            {
                seen.Add(player.Current.Name);
            }
        }

        return seen;
    }

    [Fact]
    public void Idle_LeavesTheRiderResting()
    {
        var (player, behaviour) = Create();

        Assert.Equal(["Idle"], Trace(player, behaviour, 20));
        Assert.True(player.IsResting);
    }

    [Fact]
    public void Thinking_PlaysTheGestureAgainAndAgain_WithoutSettlingInBetween()
    {
        var (player, behaviour) = Create();
        behaviour.State = CharacterState.Thinking;

        var shown = new List<string>();
        for (var i = 0; i < 12; i++)
        {
            behaviour.Advance(Frames(1));
            shown.Add($"{player.Current.Name}{player.FrameIndex}");
        }

        Assert.Equal(
            ["Thinking0", "Thinking1", "Thinking2", "Thinking3", "Thinking4", "Thinking0", "Thinking1", "Thinking2", "Thinking3", "Thinking4", "Thinking0", "Thinking1"],
            shown);
    }

    [Fact]
    public void TheWholeConversation_ThinksThenTalksThenRests()
    {
        var (player, behaviour) = Create();

        behaviour.State = CharacterState.Thinking;
        behaviour.Advance(Frames(2));
        behaviour.State = CharacterState.Talking;
        var first = Trace(player, behaviour, 15);
        behaviour.State = CharacterState.Idle;
        var second = Trace(player, behaviour, 5);

        // The thinking gesture is allowed to finish before the rider starts talking.
        Assert.Equal(["Thinking", "Talking"], first);
        Assert.Equal(["Talking", "Idle"], second);
        Assert.True(player.IsResting);
    }

    [Fact]
    public void AVeryQuickReply_IsStillSeenBeingSpoken()
    {
        var (player, behaviour) = Create();
        behaviour.State = CharacterState.Thinking;
        behaviour.Advance(Frames(1));

        // The whole reply arrives before the thinking gesture has even finished.
        behaviour.State = CharacterState.Talking;
        behaviour.State = CharacterState.Idle;

        Assert.Equal(["Thinking", "Talking", "Idle"], Trace(player, behaviour, 20));

        var talkingFrames = 0;
        (player, behaviour) = Create();
        behaviour.State = CharacterState.Talking;
        behaviour.State = CharacterState.Idle;
        for (var i = 0; i < 20; i++)
        {
            behaviour.Advance(Frames(1));
            talkingFrames += player.Current.Name == "Talking" ? 1 : 0;
        }

        Assert.InRange(talkingFrames, 6, 7);
    }

    [Fact]
    public void AReplyThatFails_GoesBackToRestingWithoutTalking()
    {
        var (player, behaviour) = Create();
        behaviour.State = CharacterState.Thinking;
        behaviour.Advance(Frames(2));

        behaviour.State = CharacterState.Idle;

        Assert.Equal(["Thinking", "Idle"], Trace(player, behaviour, 12));
    }

    [Fact]
    public void ANewQuestion_InterruptsTalkingStraightAway()
    {
        var (player, behaviour) = Create();
        behaviour.State = CharacterState.Talking;
        behaviour.Advance(Frames(1));
        behaviour.State = CharacterState.Idle;

        behaviour.State = CharacterState.Thinking;
        behaviour.Advance(Frames(1));

        Assert.Equal("Thinking", player.Current.Name);
    }

    [Fact]
    public void ATrickTheUserAsksFor_StillPlays_AndTalkingResumesAfterwards()
    {
        var (player, behaviour) = Create();
        behaviour.State = CharacterState.Talking;
        behaviour.Advance(Frames(1));

        Assert.True(player.Request("Wheelie", priority: 1));

        Assert.Equal(["Wheelie", "Talking"], Trace(player, behaviour, 10));
    }

    [Fact]
    public void ASleepingRider_WakesUpProperlyBeforeThinking()
    {
        var (player, behaviour) = Create();
        player.Request("Sleeping");
        player.Advance(Frames(3));
        Assert.Equal("Sleeping", player.Current.Name);

        behaviour.State = CharacterState.Thinking;

        Assert.Equal(["Sleeping", "SleepEnd", "Thinking"], Trace(player, behaviour, 4));
    }

    [Fact]
    public void ATrickAskedForInConversation_PlaysOnce_ThenTalkingCarriesOn()
    {
        var (player, behaviour) = Create();
        behaviour.State = CharacterState.Talking;
        behaviour.Advance(Frames(1));

        Assert.True(behaviour.Perform("Wheelie"));
        Assert.True(behaviour.IsPerforming);

        Assert.Equal(["Talking", "Wheelie", "Talking"], Trace(player, behaviour, 12));
        Assert.False(behaviour.IsPerforming);
    }

    [Fact]
    public void ATrickAskedForWhileThinking_WaitsForTheGesture_AndIsNotLostWhenTalkingStarts()
    {
        var (player, behaviour) = Create();
        behaviour.State = CharacterState.Thinking;
        behaviour.Advance(Frames(2));

        behaviour.Perform("Wheelie");
        behaviour.State = CharacterState.Talking;
        var shown = Trace(player, behaviour, 20);
        behaviour.State = CharacterState.Idle;

        Assert.Equal(["Thinking", "Wheelie", "Talking"], shown);
        Assert.Equal(["Talking", "Idle"], Trace(player, behaviour, 10));
    }

    [Fact]
    public void ALoopingTrick_IsKeptUpForAWhile_ThenEndedProperly()
    {
        var (player, behaviour) = Create();
        behaviour.TrickLoopTime = Frames(6);

        Assert.True(behaviour.Perform("Sleeping"));

        // In through the lead-in once, asleep for the set time, out through the lead-out, then resting.
        Assert.Equal(["Idle", "SleepStart", "Sleeping", "SleepEnd", "Idle"], Trace(player, behaviour, 20));
        Assert.False(behaviour.IsPerforming);
        Assert.True(player.IsResting);
    }

    [Fact]
    public void Perform_RefusesAnimationsThatCannotBeTricks()
    {
        var (player, behaviour) = Create();

        Assert.False(behaviour.Perform("Backflip"));
        Assert.False(behaviour.Perform("Idle"));
        Assert.False(behaviour.Perform("SleepStart"));
        Assert.False(behaviour.IsPerforming);
        Assert.Equal(["Idle"], Trace(player, behaviour, 5));
    }

    [Fact]
    public void ATrickFromTheMenu_ReplacesOneStillWaiting()
    {
        var (player, behaviour) = Create();
        behaviour.State = CharacterState.Thinking;
        behaviour.Advance(Frames(1));
        behaviour.Perform("Sleeping");
        behaviour.Advance(Frames(1));
        behaviour.State = CharacterState.Idle;

        // The user picks Talking from the menu before the thinking gesture ends.
        player.Request("Talking", priority: 1);

        Assert.Equal(["Thinking", "Talking"], Trace(player, behaviour, 10));
        Assert.False(behaviour.IsPerforming);
    }

    [Fact]
    public void TheRealSprites_HaveEveryAnimationTheConversationCanAskFor()
    {
        var manifest = RealManifest();
        var player = new AnimationPlayer(manifest);
        var behaviour = new CharacterBehaviour(player, manifest);

        // Kept in step by hand with RiderActions in the AI project, which this project does not refer to.
        string[] asked = ["Wheelie", "Celebration", "Revving", "Sleeping", "RidingLoop", "LookLeft", "LookRight"];

        Assert.All(asked, animation => Assert.True(behaviour.Perform(animation), animation));
    }

    [Fact]
    public void Advance_ReportsWhetherThePictureChanged()
    {
        var (_, behaviour) = Create();

        Assert.False(behaviour.Advance(Frames(0.4)));
        Assert.True(behaviour.Advance(Frames(0.7)));

        // Starting to think changes the picture even though no whole frame of time has passed.
        behaviour.State = CharacterState.Thinking;
        Assert.True(behaviour.Advance(Frames(0.1)));
    }

    [Fact]
    public void ACharacterWithoutTheseAnimations_SimplyKeepsResting()
    {
        const string plain = """
            {
              "frameWidth": 100, "frameHeight": 80, "framesPerSecond": 10, "defaultAnimation": "Idle",
              "animations": [
                { "name": "Idle", "loop": true, "frameCount": 4, "columns": 4, "right": "Right/Idle.png", "left": "Left/Idle.png" }
              ]
            }
            """;
        var (player, behaviour) = Create(plain);

        behaviour.State = CharacterState.Thinking;
        var thinking = Trace(player, behaviour, 5);
        behaviour.State = CharacterState.Talking;
        var talking = Trace(player, behaviour, 5);
        behaviour.State = CharacterState.Idle;

        Assert.Equal(["Idle"], thinking);
        Assert.Equal(["Idle"], talking);
        Assert.Equal(["Idle"], Trace(player, behaviour, 5));
    }

    [Fact]
    public void TheRealSprites_HaveTheAnimationsTheConversationUses()
    {
        var manifest = RealManifest();

        Assert.True(manifest.TryGet(CharacterBehaviour.ThinkingAnimation, out _));
        Assert.True(manifest.TryGet(CharacterBehaviour.TalkingAnimation, out var talking));
        Assert.True(talking.Loop);
    }

    private static SpriteManifest RealManifest()
    {
        var folder = AppContext.BaseDirectory;
        string? path = null;
        while (folder is not null && path is null)
        {
            var candidate = Path.Combine(folder, "Assets", "Sprites", "Rider", "sprites.json");
            path = File.Exists(candidate) ? candidate : null;
            folder = Path.GetDirectoryName(folder);
        }

        Assert.NotNull(path);
        return SpriteManifest.Parse(File.ReadAllText(path));
    }
}
