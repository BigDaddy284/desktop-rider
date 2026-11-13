# Desktop Rider 2.0 – Architecture

This document describes the parts of Desktop Rider 2.0 that exist on the `v2.0` branch. It is added to as each
part lands. So far it covers the **logic**: everything the rider knows and decides, with no drawing and no
Unity code in it.

## 1. The logic assembly

| | |
|---|---|
| Location | `unity/Assets/DesktopRider/Scripts/Logic/` |
| Unity assembly | `DesktopRider.Logic` (`DesktopRider.Logic.asmdef`) |
| Unity references | None. `noEngineReferences` is on, so it cannot use `UnityEngine` or `UnityEditor`. |
| Language and library | C# 9 and .NET Standard 2.1, which is what Unity 6 compiles. |
| Only dependency | `Newtonsoft.Json.dll`, from Unity's own package `com.unity.nuget.newtonsoft-json`. |

It is version 1's three logic projects (`DesktopRider.Core`, `DesktopRider.AI`, `DesktopRider.Animation`, as
of release 0.8.1) moved across with their behaviour unchanged. The three keep their folders and namespaces
inside the one assembly:

```
Scripts/Logic/
  Core/            DesktopRider.Core.*
    Configuration/   AppSettings, SettingsNormalizer, JsonSettingsStore, SettingsService
    Infrastructure/  AppPaths, LocalJsonFile, JsonText, ServiceSet, SingleInstanceGuard
    Logging/         ILogger, LoggerFactory, FileLoggerProvider
    Placement/       PlacementCalculator and the pixel geometry types
    Imaging/         IconLayout
    ViewModels/      OverlayViewModel, TrayViewModel, RelayCommand, ViewModelBase
    CoreServices.cs  joins the Core parts together
  AI/              DesktopRider.AI
    Ollama/          OllamaProvider and the shapes of Ollama's messages
    IAiProvider, AiProviderRegistry, AiChatService
    ConversationViewModel, SpeechBubbleViewModel, AiMenuViewModel, RiderSettingsViewModel
    RiderPersonality, RiderActions (with ActionTagFilter), RiderMemory, ConversationStore
    ProactiveComments (with QuietHours), ProactiveAnimations
    RiderServices.cs joins everything together
  Animation/       DesktopRider.Animation
    SpriteManifest, AnimationPlayer, CharacterBehaviour, IdleVariationPicker
  Polyfills/       small stand-ins for things .NET Standard 2.1 lacks (see section 4)
```

Not moved, because they are tied to Windows Presentation Foundation and are being rebuilt in Unity: the
windows (`DesktopRider.App`), the Windows integration (`DesktopRider.Desktop`) and the sprite drawing.

### What each part does

- **Settings** – `AppSettings` is the user's choices. `SettingsNormalizer` puts any out-of-range value back in
  range. `JsonSettingsStore` reads and writes `settings.json`. `SettingsService` holds the settings in use,
  saves each change and raises `Changed`.
- **Local files** – `LocalJsonFile<T>` reads and writes one JSON file. A missing or damaged file never throws;
  a damaged one is renamed `name.damaged-yyyyMMdd-HHmmss.json` and reported. Writing goes through a `.tmp`
  file so a crash cannot leave a half-written file.
- **Logging** – one plain-text file per day in `Logs\`, kept for 14 days. Conversation text is never logged.
- **Placement** – pure arithmetic for keeping the rider on screen, restoring a saved position across monitor
  changes and putting a speech bubble beside the rider.
- **AI** – `IAiProvider` is the interface a local AI program is reached through; `OllamaProvider` is the first
  one. Replies are streamed. The time limit is on *silence* (it restarts whenever Ollama sends something), a
  request can be cancelled at any moment, and every failure becomes an `AiProviderException` with a
  plain-English message. `AiProviderRegistry` finds a provider by name so others can be added. No model name
  is built in.
- **Conversation** – `ConversationViewModel` is the conversation itself: lines, sending, stopping, model
  choice, history. `SpeechBubbleViewModel` decides what the speech bubble shows and for how long.
- **Personality and memory** – `RiderPersonality` composes the instructions sent to the model. `RiderMemory`
  keeps up to 50 notes the user asked the rider to remember. `JsonConversationStore` keeps the conversation
  between sessions when the user allows it.
- **Tricks** – `RiderActions` is the catalogue of everything the rider can be asked to do (tag, animation
  name, kind, phrases that ask for it) and picks tricks, including several in a row. `ActionTagFilter` takes
  tags such as `[wheelie]` out of a reply as it streams.
- **Auto mode** – `ProactiveComments` lets the rider speak first and `ProactiveAnimations` lets it move by
  itself. Both are off until the user switches them on, respect quiet hours and make no request while off.
- **Animation rules** – `SpriteManifest` describes the animations (name, looping, lead-in and lead-out).
  `AnimationPlayer` advances frames. `CharacterBehaviour` decides what plays next: thinking, talking, queued
  tricks, idle variations. These rules do not depend on how the animation is drawn.

### How Unity code uses it

```csharp
var core = new CoreServices(AppPaths.ForCurrentUser());
await core.Settings.InitializeAsync();

var rider = new RiderServices(core);          // on Unity's main thread
await rider.Memory.LoadAsync();
await rider.Conversation.RestoreAsync();
rider.ProactiveComments.Start();
rider.ProactiveAnimations.Start();

// ... and when the application closes:
rider.Dispose();
core.Dispose();
```

`RiderServices` hands out one of each part (`Conversation`, `SpeechBubble`, `RiderSettings`, `Tray`,
`Overlay`, `Memory`, `Chat`, ...). The view models raise `PropertyChanged` and ordinary events; Unity
components subscribe to them.

**Threads.** `ConversationViewModel` deliberately continues on the thread that called it, so call it from
Unity's main thread and its events arrive there. `ProactiveComments` and `ProactiveAnimations` capture
`SynchronizationContext.Current` when first asked for and post their work back to it, so ask `RiderServices`
for them on the main thread.

## 2. Where data is kept

Exactly where version 1 kept it, so a version 1 user loses nothing:

| File | Contents |
|---|---|
| `%LOCALAPPDATA%\DesktopRider\settings.json` | The user's settings |
| `%LOCALAPPDATA%\DesktopRider\conversation.json` | The conversation, if the user keeps it |
| `%LOCALAPPDATA%\DesktopRider\memory.json` | What the rider was asked to remember |
| `%LOCALAPPDATA%\DesktopRider\Logs\desktop-rider-yyyyMMdd.log` | Logs |

Nothing leaves the computer except requests to the Ollama address the user has set.

## 3. Technical decisions

### JSON: Newtonsoft.Json instead of System.Text.Json

Version 1 used System.Text.Json, which Unity does not include. The choices were to bundle System.Text.Json
and its five supporting DLLs into the Unity project, or to use Newtonsoft.Json, which Unity publishes and
maintains as the package `com.unity.nuget.newtonsoft-json`. **Newtonsoft was chosen**: it is one supported
package rather than six hand-managed DLLs, and it works with every Unity scripting backend.

`JsonText` sets it up to match version 1's files: camelCase names, enums as camelCase words, two-space
indenting, times of day as `"22:00:00"`, dates with their offset, comments and trailing commas tolerated,
unknown properties ignored. Files are interchangeable in both directions (see TESTING.md, section 4).
The two harmless differences: Newtonsoft writes characters such as `'` and `é` as themselves where version 1
wrote `'` and `é`, and writes a whole number held as a decimal as `48.0` where version 1 wrote `48`.
Each reads the other's form.

**The Unity project must have `com.unity.nuget.newtonsoft-json` (3.2.1 or later) in `Packages/manifest.json`.**
Its Api Compatibility Level (Project Settings > Player) must stay at Unity 6's default, .NET Standard 2.1.

### No Microsoft.Extensions packages

Version 1 used Microsoft's dependency-injection and logging packages. Neither ships with Unity, so both were
replaced with a few small classes rather than bundled DLLs:

- **Logging** – `ILogger`, `ILogger<T>`, `ILoggerProvider`, `LogLevel`, `LoggerFactory`, `NullLogger<T>` and
  the `LogInformation`-style helpers, in `DesktopRider.Core.Logging`. Same names, levels and log file format.
- **Joining parts together** – `CoreServices` and `RiderServices`. Each part is made once, on first use, and
  all are closed in reverse order by `Dispose()`. They replace `AddDesktopRiderCore()` and `AddDesktopRiderAi()`.

### Language features

Version 1 was written in the newest C#. Unity compiles C# 9, so newer features were rewritten by hand with no
change of meaning: file-scoped namespaces, primary constructors, collection expressions, raw string literals,
`required` members, record structs, generated regular expressions and generated log methods. Every file
starts with `#nullable enable`, because Unity has no project-wide switch for it.

`required` properties of the animation list became `[JsonProperty(Required = ...)]`, so a list with a missing
value is still refused when it is read.

### Version number

Unity stamps no version on the code it compiles, so `AppInfo.Version` reads it from
`[assembly: AssemblyInformationalVersion("2.0.0")]` in `Scripts/Logic/AssemblyInfo.cs`. Change it there.

## 4. Polyfills

.NET Standard 2.1 lacks a few things version 1 relied on. `Scripts/Logic/Polyfills/` supplies them:

| File | Stands in for |
|---|---|
| `TimeProvider.cs` | `System.TimeProvider` and `ITimer`: the replaceable clock the tests drive by hand |
| `TimeOnly.cs` | `System.TimeOnly`: a time of day, used for quiet hours |
| `Guard.cs` | `ArgumentNullException.ThrowIfNull` and friends |
| `SharedRandom.cs` | `Random.Shared` |
| `Sequences.cs` | `MaxBy` and `MinBy` |
| `Waiting.cs` | Cancellable reading of a reply (see below) |
| `IsExternalInit.cs` | What the compiler needs for records and `init` properties |

`TimeProvider`, `ITimer` and `TimeOnly` are declared in the `DesktopRider` namespace with the same names as
the .NET types. Because all the logic lives in namespaces under `DesktopRider`, C# finds these first, so the
code reads exactly as version 1's did, and if Unity later moves to a .NET that has the real types the
polyfills can simply be deleted.

**Cancelling a read.** .NET Standard 2.1 cannot cancel `ReadLineAsync` directly. `Waiting.WithLimitAsync`
stops *waiting* the moment the time limit or the user's cancellation fires; the reply is then closed, which
ends the abandoned read. The visible behaviour (a silence time limit, immediate stop) is the same as version 1.

## 5. New in 2.0

- **Free riding** – `AppSettings.Behaviour.FreeRiding` (`"freeRiding"` in `settings.json`), **off by default**,
  exposed to a settings screen as `RiderSettingsViewModel.FreeRiding`. A version 1 settings file has no such
  line and loads with it off. The logic only stores the choice; the riding itself is Unity's job.
