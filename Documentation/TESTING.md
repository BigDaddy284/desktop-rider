# Desktop Rider 2.0 – Testing

## 1. Logic tests

The logic (`unity/Assets/DesktopRider/Scripts/Logic/`) has **634 automated tests** in
`unity/Assets/DesktopRider/Tests/EditMode/`. They are NUnit tests and can be run two ways from the same files.

### Without Unity (used for every change)

Needs the .NET 10 SDK.

```
cd logic-tests
dotnet test DesktopRider.Logic.Tests -f net10.0
```

`logic-tests/` holds two small projects that compile the very files Unity compiles:

| Project | What it proves |
|---|---|
| `DesktopRider.Logic` | The logic compiles as C# 9 against .NET Standard 2.1, with warnings treated as errors. |
| `DesktopRider.Logic.Tests` | Built for .NET 10 to run the tests, and for .NET Standard 2.1 only to prove the tests themselves compile against the library Unity uses. |

`dotnet build DesktopRider.Logic.Tests` builds both. It must finish with 0 warnings and 0 errors.

### Inside Unity

Window > General > Test Runner > EditMode > Run All. The tests are in the assembly
`DesktopRider.Logic.Tests`. The project needs the packages `com.unity.test-framework` and
`com.unity.nuget.newtonsoft-json`.

## 2. Latest results

| Date | Where | Result |
|---|---|---|
| 13 November 2026 | `dotnet test`, .NET 10 on Linux | **634 passed, 0 failed, 0 skipped** (run four times in a row) |
| 13 November 2026 | `dotnet build`, both targets | 0 warnings, 0 errors |
| – | Unity editor Test Runner | **Not yet run.** See section 5. |

Before the move, version 1's own 620 tests were run from release 0.8.1 as the baseline: 620 passed.

## 3. What the tests cover

All 620 of version 1's tests were carried over; none was dropped, skipped or weakened. 14 were added.

| Area | Tests |
|---|---|
| Core: settings, local files, logging, placement, tray menu, overlay, icon layout, paths | 153 from version 1 |
| AI: Ollama provider, chat service, conversation, speech bubble, personality, tricks, memory, history, auto mode, settings screen | 383 from version 1 |
| Animation: animation list, player, behaviour, idle variations | 84 from version 1 |
| Free riding setting | 8 new |
| Reading version 1's saved files | 5 new |
| Version number | 1 new |

Code coverage has not been measured. The requirement is that every applicable test passes.

### How the tests were converted from xUnit to NUnit

- `[Fact]` became `[Test]`; `[Theory]` with `[InlineData]` became `[TestCase]`.
- **Assertions were not rewritten.** `Tests/EditMode/Support/Assert.cs` provides checks with the same names
  and meaning as xUnit's (`Assert.Equal`, `Assert.Single`, `Assert.ThrowsAsync`, ...), reporting failures
  through NUnit. Every assertion is therefore word for word what version 1 had. It also means the tests use
  only the small part of NUnit that Unity's older copy and the current release share, and the awaitable
  checks never block the thread, which matters inside the Unity editor.
- xUnit makes a new test object for every test; NUnit makes one per class. The four test classes that kept
  per-test state in fields now set it up in `[SetUp]` and clear it in `[TearDown]`.
- The two composition tests now exercise `CoreServices` and `RiderServices`, which replaced the
  dependency-injection registrations, with the same assertions.
- `Support/Pipe.cs` and `Support/TaskCompletionSource.cs` stand in for .NET types the tests used that
  .NET Standard 2.1 does not have.

### Test data

| Path | What it is |
|---|---|
| `Tests/EditMode/Data/sprites.json` | Version 1's real list of animations. Two tests check it contains every animation the conversation can ask for. |
| `Tests/EditMode/Data/Version1/*.json` | Settings, memory and conversation files **written by the real version 1 code** (release 0.8.1), with accented letters, quotes, an emoji and non-UK time offsets. |

## 4. Saved files work in both directions

- **Version 1's files read by version 2** – automated: `Version1FilesTests` loads the files above and checks
  every value, then saves them again and checks the names and shapes are unchanged.
- **Version 2's files read by version 1** – checked by hand on 13 November 2026, because version 1's code is
  not on this branch. The logic loaded the version 1 files, changed several values, switched free riding on,
  added a note and a line containing `— ï “ ” < > &` and an emoji, and saved. Version 1's own stores
  (release 0.8.1) then loaded all three files: status `Loaded`, every value correct, the added note and line
  intact with their time offsets, and no file set aside as damaged. **Passed.**

  To repeat it: write the files with the logic's `JsonSettingsStore`, `JsonMemoryStore` and
  `JsonConversationStore`, then load the same folder with version 1's classes of the same names.

One thing to know: version 1 does not know about free riding, so if version 1 saves the settings, the
`freeRiding` line is left out and the setting returns to off.

## 5. Known limitations

- **The tests have not yet been run inside the Unity editor.** They compile against .NET Standard 2.1 and pass
  on .NET 10. Running them in the Test Runner is still needed to confirm two things that cannot be checked
  outside Unity: that the assembly definitions resolve `Newtonsoft.Json.dll` and `nunit.framework.dll`, and
  that Unity's test runner accepts the eight parameterised test methods that are also `async`.
- Unity runs the logic on Mono rather than .NET 10. No test relies on behaviour known to differ, but the Unity
  run above is what confirms it.
- `OllamaProvider` is tested against a stand-in for the network, never a real Ollama. A manual check with
  Ollama running is needed once there is a conversation window in Unity.
- A handful of tests wait on real time (tens of milliseconds) for time limits and cancellation.

## 6. Unity .meta files

Unity identifies each file by an id in a `.meta` file beside it. `logic-tests/tools/make_unity_meta.py`
writes those for the logic and its tests, working each id out from the file's path so it is always the same.
Run it after adding, moving or deleting a file there. It never changes an existing id. The shared folders
above them (`Assets/DesktopRider`, `Scripts`, `Tests`) are left for Unity to create.
