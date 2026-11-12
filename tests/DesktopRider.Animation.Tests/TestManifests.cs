using DesktopRider.Animation;

namespace DesktopRider.Animation.Tests;

internal static class TestManifests
{
    /// <summary>Ten frames a second, so one frame lasts 0.1 seconds.</summary>
    public const string Json = """
        {
          "frameWidth": 100, "frameHeight": 80, "framesPerSecond": 10, "groundY": 76, "defaultAnimation": "Idle",
          "animations": [
            { "name": "Idle", "loop": true, "frameCount": 4, "columns": 4, "right": "Right/Idle.png", "left": "Left/Idle.png" },
            { "name": "Talking", "loop": true, "frameCount": 3, "columns": 3, "right": "Right/Talking.png", "left": "Left/Talking.png" },
            { "name": "Wave", "loop": false, "frameCount": 5, "columns": 3, "right": "Right/Wave.png", "left": "Left/Wave.png" },
            { "name": "Wheelie", "loop": false, "frameCount": 6, "columns": 8, "right": "Right/Wheelie.png", "left": "Left/Wheelie.png" },
            { "name": "SleepStart", "loop": false, "frameCount": 2, "columns": 2, "right": "Right/SleepStart.png", "left": "Left/SleepStart.png" },
            { "name": "Sleeping", "loop": true, "frameCount": 4, "columns": 4, "right": "Right/Sleeping.png", "left": "Left/Sleeping.png", "enter": "SleepStart", "exit": "SleepEnd" },
            { "name": "SleepEnd", "loop": false, "frameCount": 2, "columns": 2, "right": "Right/SleepEnd.png", "left": "Left/SleepEnd.png" }
          ]
        }
        """;

    public static SpriteManifest Create() => SpriteManifest.Parse(Json);

    public static TimeSpan Frames(double count) => TimeSpan.FromSeconds(count / 10.0);
}
