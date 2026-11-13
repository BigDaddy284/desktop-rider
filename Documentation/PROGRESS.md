# Progress

Last updated: 13 November 2026

## Current task

Phases 1, 2 and 5 are complete. Phase 6 (conversation interface) is complete: built, tested automatically, and confirmed by the owner on Windows 11 with the 0.6.2 build, including the dark style, chat in bubbles and tricks on request. Phase 7 (personality and memory) is built and tested automatically in 0.7.0 and awaits the owner's check on Windows (M8). Version 0.8.0 adds, at the owner's request, tricks in a row ("do some tricks"), kinds of animation so that only tricks are picked as tricks, and the rider doing something by itself now and then while it may start conversations; built and tested automatically, awaiting the owner's check (M10). Next is Phase 8: settings and polish.

## Completed tasks

| Phase | Task | Result |
|---|---|---|
| 1 | Inspect the development environment | Done. .NET SDK 10.0.115 on Linux; WPF compiles there but cannot run. |
| 1 | Create the Windows application | Done. `DesktopRider.exe` (WPF, .NET 10). |
| 1 | Establish the solution structure | Done. Core, App and Core.Tests; see ARCHITECTURE.md. |
| 1 | Establish logging and configuration | Done. Local daily log files and a JSON settings file with repair and backup. |
| 1 | Build and test the application shell | Built and unit tested. Manual check M1 on Windows 11 is awaiting the owner. |
| 2 | Create transparent window | Built. Manual check M2 awaiting the owner. |
| 2 | Implement always-on-top | Built and unit tested (menu toggle, remembered). Manual check M2 awaiting the owner. |
| 2 | Implement dragging | Built. Manual check M2 awaiting the owner. |
| 2 | Implement desktop positioning | Built and unit tested (placement rules). Manual check M2 awaiting the owner. |
| 2 | Implement system tray controls | Built and unit tested: icon beside the clock with show, hide, always on top, move back to the corner and exit; hiding is remembered. The owner checked the 0.4.0 build on Windows 11 and reported it looks and works well. |
| 3 | Inspect existing reference photographs | Done. 28 photographs received in three batches; see the visual reference summary. |
| 3 | Character designs | Done. The owner supplied their own 3D models; colours and graphics were applied to match the photographs and the owner approved them. |
| 3 | Sprite conventions and rendering | Done. 15 animations rendered in both directions; see SPRITE_SPECIFICATION.md. |
| 4 | Animation playback, transitions, priorities | Built and unit tested (`DesktopRider.Animation`). |
| 4 | Initial animations | All thirteen from DESIGN.md 3.6 built in Blender and approved by the owner, plus sleep start and end. |
| 4 | Animated rider in the window | Built. The owner ran it on Windows 11 and reported it looks and works well; see M3 in TESTING.md. |
| 5 | Implement provider abstraction | Built and unit tested (`IAiProvider`, `AiProviderRegistry`, `AiChatService` in `DesktopRider.AI`). |
| 5 | Connect to Ollama | Built and unit tested, including a round trip over a real local port. The owner confirmed it on Windows 11 with the 0.5.1 build. |
| 5 | Discover models | Built and unit tested; the user picks the model from the icon's menu. Nothing is assumed. The owner confirmed it on Windows 11 with the 0.5.1 build. |
| 5 | Send messages and receive streaming responses | Built and unit tested. First tried through a temporary **Ask the rider to say hello** menu line, since replaced by the conversation window. The owner confirmed it on Windows 11 with the 0.5.1 build. |
| 5 | Handle errors and cancellation | Built and unit tested: Ollama not running, model missing, time limits, broken connections, stopping a reply, closing during a reply. Tested automatically only; not yet tried by hand. |
| 6 | Implement speech bubbles | Built and unit tested (`SpeechBubbleViewModel`, placement rules). The window itself awaits manual check M6. |
| 6 | Implement conversation window | Built and unit tested (`ConversationViewModel`): history, send, stop, clear, working indicator, current model with a picker, connection status. The window itself awaits manual check M6. |
| 6 | Implement text input | Built: Enter sends, Shift+Enter starts a new line, Esc closes. Awaits manual check M6. |
| 6 | Connect character animation states to conversation events | Built and unit tested (`CharacterBehaviour`): thinking, talking, back to rest. Awaits manual check M6. |
| 6 | Interface style and bubble chat (owner's request after trying 0.6.0) | Built: dark Windows 11 style for the rider's menu, the icon's menu, the conversation window and bubbles; a **Chat in bubbles** switch with a typing bubble below the rider. Logic unit tested; the look awaits the owner's check (M6, M7). |
| 6 | Tricks on request (owner's request) | Built and unit tested: asking the rider in conversation for a wheelie, a rev, a celebration, a nap, a ride or a look left or right plays that animation. Awaits the owner's check (M7 steps 14 to 16). |
| 7 | Implement configurable personality | Built and unit tested (`RiderPersonality`, `RiderSettingsViewModel`): a **Rider settings** window with how the rider talks (cheeky, friendly, serious), short or detailed replies, what the rider calls you, and an optional description in your own words. The window itself awaits manual check M8. |
| 7 | Implement local conversation history | Built and unit tested (`JsonConversationStore`, `ConversationViewModel`): the conversation is kept in `conversation.json` on this computer and is back next time; it can be switched off, kept for a week to a year, and deleted. The owner has not yet tried it (M8). |
| 7 | Implement optional memory | Built and unit tested (`RiderMemory`, `JsonMemoryStore`): the rider remembers short notes the user asks it to ("remember that…" in conversation, or typed in Rider settings), kept in `memory.json` on this computer. It can be switched off, and notes can be forgotten one at a time or all at once. The owner has not yet tried it (M8). |
| 7 | Implement proactive behaviour controls | Built and unit tested (`ProactiveComments`, `QuietHours`): off by default with no timer and no AI request; when switched on, a remark after the chosen time, quiet hours, a one-hour mute on the rider's menu, and silence after three unanswered remarks. The owner has not yet tried it (M8). |
| 4 | Jumps, freestyle tricks, whoops and a stoppie (owner's request) | Built in 0.6.3 and combined with Phase 7 in 0.7.1: 18 new animations rendered from the owner's own models in both directions (jump, whip, scrub, backflip, superman, superman seat grab, no hander, no footer, nothing, heel clicker, can-can, nac-nac, lazy boy, one hander, indian air, rock solid, whoops, stoppie), larger frames with their own window so ramps and height fit, and all of them available on request and to the AI. Manifest, frame rules and requests are unit tested. How they look on Windows awaits the owner's check (M9), and the ramps, mounds and whoops are new artwork awaiting approval. |
| 4 | The rider understanding its animations (owner's request) | Built in 0.8.0. Every action has a kind (trick, on the bike, gesture) and the AI is told which is which. "Do some tricks" plays three or four different tricks, and only tricks, starting with any the rider names in its answer; tricks queue and play one after another. While the rider may start conversations it also does a random animation every few minutes without using the AI. Fixes the owner's report of 13 November: asked for some tricks, the rider said "how about a no hander" and did one wheelie. Unit tested; awaits the owner's check (M10). |
| 3 | Organise all supplied references | Done. Stored privately by the owner; folder structure and written summary are in `References/`. |

## Latest results

| Check | Result |
|---|---|
| Build (`dotnet build`, warnings as errors) | Succeeded, 0 warnings, 0 errors |
| Tests (`dotnet test`) | 620 passed, 0 failed, 0 skipped (Core 153, Animation 84, AI 383) |
| GitHub Actions build and test on Windows | Passed for Phase 1; runs again on each push |
| Standalone Windows build (`dotnet publish`, win-x64) | Succeeded |
| Manual checks on Windows 11 | The owner ran the 0.3.0 test build: it starts, shows the animated rider and works. M1 to M3 not yet reported step by step. |

## Remaining tasks

- **Phase 4 – Animation system:** connect animations to behaviour (moving around the screen, reacting to the user) as those features arrive.
- **Phase 8 – Settings and polish.**
- **Phase 9 – Final validation.**

## Blockers

- None.

## Known issues

- On 13 November 2026 the owner tried the 0.7.0 build (Phase 7) and the 0.6.3 build (jumps and tricks) separately and reported that both work great. The steps of M8 and M9 were not reported one by one. 0.7.1 is the first build with both together and has not yet been tried by the owner.
- On 13 November 2026 the owner tried 0.8.0 and reported replies with a word missing ("fancy a ?", "Let's go for a !") and a leading dash. The model was using the tag as a word. Fixed in 0.8.1; whether a given model now keeps to short replies such as "Sure, watch this!" depends on the model, but the missing word and the dash can no longer appear. The rest of M10 was not reported.
- 0.8.1 also hides the speech bubble during tricks, at the owner's request. 0.8.1 has not been run by the developer or yet tried by the owner. In particular, reading each queued jump's pictures in the background between tricks is new window code that only M10 can confirm, and how well a given AI model uses the new tags is unknown; Desktop Rider does not depend on it.
- The window that shows jumps and tricks larger than the rider's usual frame, and loading those large sheets in the background, have never been run by the developer (Linux build machine). They compile and the frame and origin rules are tested, but how they look on Windows depends on manual check M9.
- The new jumps and tricks were animated from descriptions of the techniques and simple flight physics, not traced frame by frame from film, and use a gentler gravity so a jump fits on a desktop. Timing and body positions may need adjusting after the owner has watched them.
- The conversation window, the speech bubble and click-versus-drag on the rider have never been run by the developer (Linux build machine). They compile and their logic is tested, but how they look and behave on Windows depends on manual check M6.
- The owner tried 0.6.0 and reported the conversation working, then asked for the dark Windows 11 style and a bubble chat mode before moving on; these are in 0.6.1. The dark style has never been seen by the developer, so its look is unconfirmed until the owner tries it.
- The owner tried 0.6.1 and reported it looks OK, but in bubble mode clicking the rider hid only the typing bubble and left the rider's answer up. 0.6.2 puts a finished answer away with the typing bubble (regression test in `SpeechBubbleViewModelTests`).
- Whether the AI itself chooses to do a trick unprompted depends on the model following an instruction, which small models may not. Plain requests such as "do a wheelie" do not depend on that.
- In the default corner the typing bubble only just fits below the rider. With Windows display scaling above 100% it will not fit there and appears beside or above the rider instead; dragging the rider up a little puts it below.
- The Rider settings window (0.7.0) has never been seen by the developer. It compiles and everything it decides is tested, but its look and layout are unconfirmed until the owner tries it (M8).
- What the rider says when it starts a conversation, and how well it uses a remembered fact or the chosen way of talking, depends on the AI model. Small models may follow those instructions loosely.
- "Remember that…" is recognised from a fixed set of wordings. Something phrased very differently is not stored; the settings window's **Add** box always works.
- The owner tried the 0.5.0 build: choosing **Check the AI connection** appeared to do nothing. Results were shown as Windows notifications, which Windows can hide. 0.5.1 shows them in Desktop Rider's own small window instead, starting the moment the menu item is clicked. The owner confirmed 0.5.1 works.
- The developer can only run the AI code against a stand-in for Ollama. The owner has confirmed discovery, model choice and a streamed reply against a real Ollama; the failure and stop cases have only been tested automatically.
- The Ollama address, temperature and waiting time have no settings screen yet (Phase 8); they can only be changed in the settings file.
- The window has never been run by the developer, because the build machine is Linux. Everything visual depends on manual checks M1 and M2.


- Multiple monitors have not been tried on real hardware. The owner uses a single 49-inch ultrawide monitor and has no second screen, so M2 steps 9 and 10 cannot be done there. The placement rules for several monitors are covered by automated tests only.

## Technical decisions

Recorded in ARCHITECTURE.md.

## Next task

Phase 8: Settings and polish (complete the settings interface, start-up options, transitions and visual polish, packaging). 

---

# Version 2.0 (Unity rebuild): progress

Everything above this line describes version 1 (the Windows application under `src/`), which is kept unchanged.
Everything below describes the version 2.0 rebuild in Unity.

Each part of the 2.0 rebuild records its progress here.

### Logic moved from version 1 into Unity

**Status: complete, awaiting its first run inside the Unity editor.**

#### Completed

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

#### Test and build results (13 November 2026)

- `dotnet test`: 634 passed, 0 failed, 0 skipped.
- `dotnet build` (C# 9, .NET Standard 2.1, warnings as errors): 0 warnings, 0 errors.
- Version 1 baseline before the move: 620 passed.
- Saved files checked in both directions between version 1 and the new logic.

#### Technical decisions

Recorded in ARCHITECTURE.md, section 3: Newtonsoft.Json in place of System.Text.Json; small built-in logging
and composition classes in place of the Microsoft.Extensions packages; polyfills for what .NET Standard 2.1
lacks; the version number stated in code.

#### Known issues and what is still needed

- The tests have not been run in the Unity editor's Test Runner yet (TESTING.md, section 5).
- The Unity project needs `com.unity.nuget.newtonsoft-json` in `Packages/manifest.json`.
- `OllamaProvider` has not been tried against a real Ollama from Unity.

#### Blockers

None.

#### Next

Run the EditMode tests in Unity once the code is copied into the project, then build the conversation and
settings windows on top of `RiderServices`.

## Phase 1: overlay proof (in progress, 13 November 2026)

- The Unity project's source is in `unity/` (Unity 6000.6.6f1, URP 17.6).
- `OverlayWindow` makes the Unity window a see-through, click-through, always-on-top layer over the main screen.
  Clicks are kept only while the mouse is over the rider.
- `FreeRider` and `RiderSteering` make the bike wander the screen by itself, slowing to stop on each target,
  leaning into turns and dragging with the mouse. Frame rate drops to 15 a second while it rests.
- The current Blender model is in as a rough, untextured stand-in (`Models/Proof`), with its 15 clips cut at the
  ranges recorded in the Blender file. The textured model comes from the graphics workstream.
- Unity tests: 650 passed, 0 failed. Build: succeeded, 0 errors, 0 warnings.
- Measurements on the development PC (Direct3D 11, 1-minute runs, processor time as a share of one core):

  | Set-up | Average processor | Memory |
  |---|---|---|
  | Whole-screen window, 60 riding / 15 resting frames a second | 26.8 % | about 245 MB |
  | Same, one job worker thread | 19.6 % | |
  | Same, 5 frames a second throughout | 2.8 % | |
  | Small window that follows the rider, 30 / 10 frames a second, one worker (now the default) | 10.3 % (1.3 % fully at rest) | about 240 MB |

  The cost is about 5 ms of processor time per frame drawn, almost independent of window size, so frame rate is
  what matters. The light's real-time shadow map is off (the bike carries its own shadow) and anti-aliasing is 2x.
- Memory over 24 minutes: 231 MB at start, 242 MB after 2 minutes, then flat at 244 MB to the end. No leak. Average
  processor use over the same run: 9.6 % of one core (286 samples).
- Boss checked the proof on the PC: the rider rides about and can be picked up and moved.
- Still to confirm: clicks reach other windows when the mouse is not over the rider.

### Textured model in the overlay (13 November 2026)

- The graphics workstream's `Models/DesktopRider.fbx` (bike and rider rigs, 15 clips as separate takes) and its two
  texture atlases are now the model the overlay uses. The proof model stays as a stand-in if the textured one is missing.
- `ProofModelImport` imports it as legacy animation, names each clip after its take and sets which clips loop.
- The bike and rider rigs are separate roots under one model object, so moving the object moves both and every clip
  plays on both together.
- Checked with the new `snapshot` bridge command: 38 materials, all URP Lit and all textured (atlases imported at
  2048 pixels), nose facing forward, 0.88 x 1.55 x 1.90 m.
- Per Boss, no models, textures or renders go to GitHub: `Models/` stays on the PC only, and the proof model was taken
  out of the repository. Setting up the scene in a fresh clone needs those files copied in.
- Unity tests: 662 passed, 0 failed (one timing test failed once during the import and passed on the re-run).
  Build: succeeded, 0 errors, 0 warnings.
- First run with the textured model: about 14 % of one core while riding (was about 10 % with the plain model),
  memory about 245 MB. To look at again when the frame-rate work is revisited.
