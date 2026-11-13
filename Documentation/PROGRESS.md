# Desktop Rider 2.0 – Progress

Each part of the 2.0 rebuild records its progress here.

## Logic moved from version 1 into Unity

**Status: complete, awaiting its first run inside the Unity editor.**

### Completed

- Version 1's `DesktopRider.Core`, `DesktopRider.AI` and `DesktopRider.Animation` (release 0.8.1) moved to
  `unity/Assets/DesktopRider/Scripts/Logic/` as C# 9 / .NET Standard 2.1 with no Unity references:
  settings, local files, logging, placement, tray and overlay view models, the AI provider interface, the
  Ollama provider, provider registry, chat service, conversation, speech bubble, personality, tricks and tag
  filter, memory and conversation stores, the rider speaking and moving by itself, quiet hours, the settings
  view model, and the animation rules.
- All 620 of version 1's tests converted to NUnit in `unity/Assets/DesktopRider/Tests/EditMode/`.
- New setting: free riding, off by default.
- `logic-tests/` projects so the same files build and test without Unity.
- Stable Unity `.meta` files for both folders.

### Test and build results (13 November 2026)

- `dotnet test`: 634 passed, 0 failed, 0 skipped.
- `dotnet build` (C# 9, .NET Standard 2.1, warnings as errors): 0 warnings, 0 errors.
- Version 1 baseline before the move: 620 passed.
- Saved files checked in both directions between version 1 and the new logic.

### Technical decisions

Recorded in ARCHITECTURE.md, section 3: Newtonsoft.Json in place of System.Text.Json; small built-in logging
and composition classes in place of the Microsoft.Extensions packages; polyfills for what .NET Standard 2.1
lacks; the version number stated in code.

### Known issues and what is still needed

- The tests have not been run in the Unity editor's Test Runner yet (TESTING.md, section 5).
- The Unity project needs `com.unity.nuget.newtonsoft-json` in `Packages/manifest.json`.
- `OllamaProvider` has not been tried against a real Ollama from Unity.

### Blockers

None.

### Next

Run the EditMode tests in Unity once the code is copied into the project, then build the conversation and
settings windows on top of `RiderServices`.
