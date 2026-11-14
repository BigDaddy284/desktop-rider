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
- Fixed wrong kit colours (dark red jersey, blue plastics): the FBX carries each material's old flat colour as
  well as the texture and Unity multiplied them. The importer now sets the colour to white wherever a texture is
  present, so the kit matches Blender (light blue, pink and white).
- The 18 trick clips in `DesktopRider_Tricks.fbx` (rigs only) import as play-once legacy takes; using them, with the
  forward travel from `props_layout.json`, is part of the tricks phase.
- Checked with the new `snapshot` bridge command: 38 materials, all URP Lit and all textured (atlases imported at
  2048 pixels), nose facing forward, 0.88 x 1.55 x 1.90 m.
- Per Boss, no models, textures or renders go to GitHub: `Models/` stays on the PC only, and the proof model was taken
  out of the repository. Setting up the scene in a fresh clone needs those files copied in.
- Unity tests: 662 passed, 0 failed (one timing test failed once during the import and passed on the re-run).
  Build: succeeded, 0 errors, 0 warnings.
- First run with the textured model: about 14 % of one core while riding (was about 10 % with the plain model),
  memory about 245 MB. To look at again when the frame-rate work is revisited.

### 3D bike and rider for Unity, with the race-kit graphics

**Status: complete. Bike, rider, 15 clips, 18 trick clips and 9 props are in the Unity project and approved by the owner (13 Nov 2026). A final approved copy is kept on the owner's PC.**

#### Completed (13 November 2026)

- UV maps added to the owner's bike and rider. Neither model had any before; their graphics were projected photographs, which Unity cannot use. The earlier painted look was baked into one 4096 texture per model. Before and after renders matched, with no seams.
- The owner's vector kit `Harvey28_v2.pdf` was fitted to the bike's plastics. The JS Financial Solutions strips go on the swingarm, pink on the left and blue on the right, as the owner asked. The rider's name was taken off the side panels and the 28 enlarged, at the owner's request. Details are in MODEL_SPECIFICATION.md.
- The helmet was reshaped with the owner in Blender, using temporary handles, and its joins were made flush. Gloves, helmet top and peak edge were changed to match the correct-kit riding photographs.
- The owner approved the 9-angle renders. The scene was saved as its own working file, and the master Blender file was never changed.
- `DesktopRider.fbx` (meshes, both rigs, 15 clips with IK baked) and the textures were exported to `Assets/DesktopRider/Models/`.
- The nine ramp, landing and whoops props were rebuilt as clean meshes for real-time use and exported with `props_layout.json`.
- `Tools/Blender/build_unity_props.py` and `Tools/Blender/verify_models.py` added.
- `DesktopRider_Tricks.fbx` exported with the 18 trick clips on copies of the two rigs, keyed for the right side. `Tools/Blender/export_models.py` and `Tools/Blender/verify_tricks.py` added.
- At the owner's request the wheel hubs were made the kit's pink and the rims the kit's blue. The owner pointed out that the discs on the hub ends are brake discs, so they were painted as steel brake discs from the owner's reference photo. Texture change only; approved by the owner.
- At the owner's request the swingarm was made black (the JS strips kept) and the rear shock spring steel grey. Texture change only; `verify_models.py` still 19 PASS, 0 FAIL.
- At the owner's request the side-panel 28 was moved 4 cm forward and the swingarm strips were straightened onto the arm's centre line. Texture change only.
- At the owner's request the upper fork tubes were extended to meet the fork lowers (64 vertices). `DesktopRider.fbx` exported again; `verify_models.py` 19 PASS, 0 FAIL, with the check now allowing only the listed fork vertices to differ from the master.
- At the owner's request the fork lowers were made grey down to the axle, with the pink kept only on the front of each guard. Texture change only; 19 PASS, 0 FAIL.
- At the owner's request the fork was remodelled into full-length tubes with front-only guards, and the chain was closed round both sprockets with its top run straightened. `DesktopRider.fbx` exported again; 19 PASS, 0 FAIL.
- At the owner's request the grey back of each fork guard was deleted so the fork tube shows. `DesktopRider.fbx` exported again; 19 PASS, 0 FAIL.
- The helmet's right side was painted from the owner's photos of the same Fox V1 helmet and approved (23:48). Texture change only; 19 PASS, 0 FAIL.
- The slim tube was then centred, and the fat tube's bottom ring straightened so the gap is an even 6.1 mm all round. The owner approved the bike as it now stands (13 Nov 2026, 23:22). An approved copy of the Blender file, models and textures is kept on the owner's PC.
- At the owner's request the fork steps down to a slimmer lower tube (80% of the upper) from just above the guards. `DesktopRider.fbx` exported again; 19 PASS, 0 FAIL.

#### Test results

- `verify_models.py` on the owner's PC: 19 PASS, 0 FAIL (see TESTING.md, "3D model checks").
- `verify_tricks.py` on the owner's PC: 17 PASS, 0 FAIL.
- No C# changed in this part.

#### Technical decisions

- Models, textures and renders are kept out of the repository (owner's decision). `.gitignore` excludes `unity/Assets/DesktopRider/Models/`, `kit-fit-work/` and `.blend` files.
- In the FBX the rider rig sits beside the bike rig and follows the `chassis` bone through keys, instead of being nested under it. Blender cannot write a readable nested armature. The Unity side moves both under one parent.
- Helmet shape changes were kept as adjustable modifiers in the working Blender file and baked only into the exported mesh.
- The trick clips keep the bike in place and the ground moving, as in version 1. The forward travel per frame is in `Props/props_layout.json`, not in the bones.
- The brake discs are round in the mesh, so their wavy edge and cut-outs are painted on. The models are not reshaped without the owner asking.

#### Next

- Optional, if the owner asks: repaint the peak's right side to the black-with-stripes look of the real helmet.

## Phase 2: desktop app basics for the 3D rider (in progress, 14 November 2026)

Order of the remaining phases, as Boss set it on 14 November 2026: desktop basics (this phase), then parked mode and
the free-riding setting, the riding style, tricks and ramps, the chat (Boss has a different idea for it, still using
Ollama; to be discussed when it is reached), then settings and the final check.

Update, 14 November 2026: Boss is building a separate project that the chat will use when it is ready, so the chat
phase is on hold. The existing Ollama chat code stays in place untouched, and work moves on to settings and the final
check, with no chat options in the settings window for now.

#### Settings and final check (started 14 November 2026)

- Tricks by himself (Boss chose a setting): Never, Sometimes (one every 5 to 10 minutes, the default) or Often (2 to 4
  minutes). `OwnTrickTimer` (Logic/Tricks) times them; AppHost looks once a second and retries 20 s later when he is
  hidden, held, busy or short of room. A trick asked for from the menu restarts the wait.
- Settings window ("Settings..." in the tray menu, or `DesktopRider3D.exe --open-settings`): size, animation speed, ride
  around freely, tricks by himself, little movements at rest, always on top, start with Windows, and "Move him back to
  the corner". Changes apply at once; no Save button. `SettingsPanel` (the lines) and `SettingsLayout` (where everything
  goes, hit testing, sliders) are plain logic with tests; `SettingsWindow` (Overlay) draws it with Windows' own calls
  in the dark Windows 11 style (GDI+ for smooth shapes, ClearType words, dark title bar, round corners), sized for the
  screen's scaling. Seen on Boss's PC through the new editor-bridge `capture` command (PrintWindow).
- Always on top was ticked at start-up but not working until switched off and on (Boss). Cause: a resize asked for
  before the window was set up went through Unity, and Unity's resize puts the window back in the ordinary window order
  after it had been made always-on-top; also the click-through switch rewrote the window's extended style without the
  always-on-top mark. Fixed both, and the place is put back 1 s and 5 s after start-up.
- Free riding flows (Boss: three to five times the distance, fewer stops): each ride now goes through 3 to 5 places
  without stopping, picking the next one before he would start braking and preferring one that needs no sharp turn,
  then he rests.
- Dirt while riding (Boss): a lighter trail of dirt now comes off the rear wheel the whole time he rides, more the
  faster he goes, and two to three times as much, thrown to the outside, when he leans into a turn; none during tricks
  or at rest. The bigger burst and the smoke still play when he sets off (`StartEffects`).
- Always on top, second report (Boss: still behind apps at start-up). The two re-applies were not enough: the player
  log showed Windows had taken the always-on-top mark off again after them. `OverlayWindow` now checks every 1.5 s
  (one cheap look at the window order, nothing redrawn) whether the mark is gone or an ordinary window sits above him,
  and puts him back the way the switch does (off, then on). Other always-on-top windows are left alone. On the next run
  the log showed one such fix at start-up and none after.
- A random trick where the jump and landing appeared but he rode straight through them (Boss). Cause: the trick began
  while he was cornering; the corner pose sits on an animation layer above the trick clip and is not updated during a
  trick, so a leg half out held the riding pose over the whole trick. `BeginTrick` now clears the corner pose at once
  (`CornerBlend.Reset`, with a test). Tricks from the menu mostly started at rest, which is why it had not shown before.
- Editor bridge: `capture <window title>`, `winclick <title>|x|y`, `playerlog`; `trick` also lists the clips playing (is the program running, and the end of
  its Unity log).
- Seen in the player log: Unity's own cloud services try to connect at start-up (and fail on this PC). To check in
  the final check: turn off Unity's analytics and diagnostics in the build so nothing leaves the PC.

#### Done so far (written overnight with the PC off; Unity side not yet compiled in Unity)

- `RidingArea` (Logic/Core/Desktop): every screen's work area in desktop pixels, starting at 0,0 even when a screen is
  left of or above the main one. Keeps the rider off gaps between screens of different sizes, picks places to ride to,
  usually on the same screen and sometimes on another.
- `TrayMenuPlan`: turns the tray view model's menu into numbered lines for a Windows pop-up menu.
- `StartWithWindows`: the "Start with Windows" tray line and the per-user sign-in list (Run key), off by default.
  Fixes the stored path if the program moves.
- `FreeRidingMenu`: the "Ride around freely" tray line. Off by default, as the plan says: the rider stays parked.
- Unity side: `TrayIcon` (Shell_NotifyIcon with a hidden window; left click hides or shows, right click opens the
  menu, the hidden-rider reminder, icon re-added if Explorer restarts, screen-change notices), `RiderIcon` (the icon is
  drawn from the 3D model at start-up), `WindowsStartupRegistry`, `MainThread`, and `AppHost`, which loads the settings
  and applies them: shown or hidden (hidden draws nothing), always on top, rider size, free riding or parked, and the
  last position. `OverlayWindow` now covers every screen and scales with Windows' screen scaling.
- Version 2 keeps its settings in `%LOCALAPPDATA%\DesktopRider3D`, separate from version 1's, so the two can run side by
  side during development without changing each other's settings.
- Logic tests: 667 passed, 0 failed (`dotnet test` in `logic-tests`). The Unity scripts compile against the Unity
  2021.3 module references outside Unity; the real check in Unity 6 is still to do.

- Parked mode: with free riding off the rider stays where it was put. At rest (parked, or stopped between rides)
  `ModelAnimationDirector` (Logic/Animation) picks the 3D clips with version 1's rules: the resting loop, a small
  movement every 8 to 20 seconds (IdleVariation, LookLeft, LookRight), thinking and talking, tricks one after another,
  and sleep through its start and end clips. While the bike moves the riding loop plays. The idle-animations and
  animation-speed settings apply. Logic tests: 678 passed, 0 failed.

- Ready for phase 4 (tricks and ramps), written while the PC was off:
  - `References/Terrain/README.md`: research on real motocross and freestyle jumps and track terrain, with sources, scaled to
    the KTM 50 world. Its key findings:
    - A 30° face gives the rider's own "about a wheel's height" of air.
    - Take-offs use a tightening curve, a straight face and a lip.
    - Landings have a flat deck, a rounded knuckle, a 10–20° face and a run-out.
    - Dirt jumps have sloping sides, never vertical walls.
    - Whoops are cupped and uneven.
    - The ground needs ruts, braking bumps, damp and dry dirt, clods and roost.
  - `Tools/Blender/terrain_shapes.py`: the shapes behind those findings, as plain maths the prop builder can use, with 27
    tests.
  - `Logic/Tricks`: reading props_layout.json, placing the props ahead of the rider, moving the rider by each frame's
    travel, choosing a heading that keeps the whole trick on a screen, and choosing the next trick.
  - Unity: `TrickRunner` adds the tricks model's takes to the rider's clips, puts the props down, plays the trick and
    widens the window over the trick while it runs. `FreeRider` hands control to it and takes it back.
  - The bridge has a new `trick [name]` command for testing in play mode.
  - Logic tests: 695 passed, 0 failed.
  - Still to do in phase 4:
    - Rebuild the props with the new shapes in Blender.
    - Run the manual check in TESTING §10.
    - Decide with Boss when tricks happen by themselves.

- 14 November 2026, with Boss testing on the PC:
  - Tricks now start from anywhere: if there is no room where the rider is, it rides to the nearest place that has room
    (`TrickRun.PlanStart`), then goes. The tray menu has a "Do a trick" line.
  - White box and lost always-on-top after a trick: `Screen.SetResolution` turns the window back into an ordinary
    opaque, titled, non-topmost window. A first fix restyled it afterwards, which left a white flash at every resize.
    Now the overlay resizes its own window with `SetWindowPos` (the player is built with a resizable window so Unity
    follows) and only falls back to `Screen.SetResolution` if Unity does not follow within ten frames.
  - Dirt (Boss: "still looks too clean and CGI"). Close-up renders in Unity (`prop` bridge command) showed why: the
    colours were pale tan (linear values treated as if they were sRGB), the mounds were flat-shaded facets, and wet and
    dry dirt met in straight lines along the mesh's faces. Now:
    - real dark track-dirt colours, clods, ruts, tyre prints and cracks at a size that shows on the desktop, each with
      a normal map so they catch the light (`dirt_texture.py`);
    - one texture per prop, blended pixel by pixel from damp, dry and loose dirt by how each spot is worn, with ragged,
      faded edges (`dirt_bake.py`);
    - smooth-shaded mounds with lumpy, rain-rilled sides (`prop_detail.py`).
  - Python tests: 59 passed (`python3 -m unittest discover -s Tools/Blender/tests`).
  - Riding style from the owner's photographs and videos (`Tools/Blender/riding_style.py`, exported as
    `DesktopRider_Style.fbx`): seated at the front of the seat, a ride-off clip when setting off, and corner poses with
    the inside leg out, blended in by the turn still to come (`CornerBlend`) so the leg comes out as the turn starts, not
    half way through. Dirt spray and blue smoke at the rear wheel when setting off (`StartEffects`). Boss had the
    feet-down rest and start poses taken out.
  - Tricks cut off at the edge of their window (Boss: the front wheel at the end, the helmet at the top of jumps). Two
    causes: the room above the bike was a fixed 1.8 m (tricks reach up to 3.9 m), and the window was a square as big as
    the longest side, which for a long jump is taller than a 1080-pixel screen, so Windows cut it short. Now each trick's
    room is measured from its clip, frame by frame (`TrickExtents`), and the window is the shape of the trick, kept on the
    desktop (`FreeRider.TrickWindow`). All 18 tricks checked frame by frame with the new Trick Viewer: nothing outside.
  - Unity tests: 754 passed, 0 failed. Build succeeded.
  - Trick review with Boss (14 November):
    - NoFooter taken out. The old HeelClicker was really a bar hop (legs between the arms): renamed BarHop. A new
      HeelClicker brings the legs round the outside of the arms (knee pole targets, added only in the export session)
      and clicks the heels once in front of the chest.
    - Whip and Scrub rebuilt from written technique guides: both start on the face; the whip lays the bike near flat
      with the back end swung out about the steering head and the rear wheel slowing then spinning up; the scrub
      leans over on the face, keeps 62% of the kick so it stays low, and is upright again for the landing.
    - NacNac: the back of the bike kicks out away from the rider. Indian Air: body twisted onto its side. Nothing:
      legs trail less far back. Stoppie and every other trick: the hips move just enough that a hand holding the
      bar can always reach it (`keep_hands_on`).
    - New WhoopsWheelie from Boss's video: front wheel up through the whole whoop section, rider standing.
    - `Tools/Blender/trick_check.py` renders any trick from four angles and measures hand and foot gaps.
    - Unity tests: 755 passed. Trick Viewer check: 19 tricks, 0 frames outside the window. Build succeeded.
  - Second review (14 November): Whip's back end now leads the swing and comes back first (smoother curves). Scrub
    uses the Whip's jump and props, coming in 38% faster with about half the height. NacNac rebuilt from Boss's video
    and photos: the bike swings nearly side-on (72°) under a rider who keeps facing forward. WhoopsWheelie: the first
    whoop kicks the front up and the rear wheel bounces from top to top.
  - Third review (14 November): Whip and Scrub now throw the back end out to the high side, away from the lean (Boss's
    picture with the green line); checked by measuring the rear axle against the rider's lean at every frame. The
    Scrub's jump between frames at the lip is gone: it now turns about its tyres the whole way and its lean eases in
    and out (trick_check.py prints the worst frame-to-frame change of axle motion as "jerk"; mid-air spikes went from
    61 to 16 mm). WhoopsWheelie: the rear wheel lands on the face of each whoop about 8 cm below the top and the
    suspension soaks each hit. Unity tests 755/755, every trick inside its window, build OK.
  - Fourth review (14 November), WhoopsWheelie from Boss's rollers video: the rider's body now glides while the bike
    pumps beneath it. After the path is built, absorb_whoops() moves the hips against the bike's rise and fall (about
    one whoop's worth of smoothing) and folds the back forward as the bike comes up into him; the stance is a little
    lower and further forward so the arms have room to straighten when the bike drops away. Measured per whoop: the
    bike moves about 9 cm, the head about 1 cm (it was moving with the bike); hands stay on the bars. The rear wheel
    now meets each face about half way up (16 cm below the top). Unity tests 755/755, all tricks inside their windows.

#### Still to do in this phase

- Compile and run the Unity tests on the PC, build, and check by hand: tray menu, hide and show, always on top,
  two screens, 150% scaling, start with Windows, position kept after a restart.
