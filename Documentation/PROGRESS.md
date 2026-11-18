# Progress

Last updated: 17 November 2026

## Current task

Phases 1, 2 and 5 are complete. Phase 6 (conversation interface) is complete: built, tested automatically, and confirmed by the owner on Windows 11 with the 0.6.2 build, including the dark style, chat in bubbles and tricks on request. Phase 7 (personality and memory) is built and tested automatically in 0.7.0 and awaits the owner's check on Windows (M8). Version 0.8.0 adds, at the owner's request, tricks in a row ("do some tricks"), kinds of animation so that only tricks are picked as tricks, and the rider doing something by itself now and then while it may start conversations; built and tested automatically, awaiting the owner's check (M10). Next is Phase 8: settings and polish.

Second character set (sidecar outfit 41): paint approved. The owner rejected the first pass of the 28 motions; the second pass (17 Nov 2026) rebuilds every crew pose on the rig from his race photos, runs at real speed with real gravity, uses a sidecar-sized double jump and bigger rollers, and checks every frame for wheels in the ground and bodies through the bike. Corner poses redone from the owner's two corner photos (17 Nov 2026). All 27 motions approved by the owner in the Unity sidecar viewer on 17 Nov 2026 (see `Documentation/ASSET_INDEX.md`; snapshot in project files sidecar/approved/2026-11-17-all/). Version 0.9.0 brings it into the app as a second character, **Sidecar 41**, chosen in Rider settings; the sprite sheets await the owner's look on Windows (M11). Video of real sidecar runs would let the jumps and rollers be matched more closely.

3D app, version 2.1 (17 Nov 2026): at the owner's choice the sidecar goes into the 3D Unity Desktop Rider (version 2, no AI), not the 2D app. Settings gain **Who rides** (Harvey or Sidecar 41) and **Riding speed** (0.5x to 2x). The owner saw the first test sidecar circle forever because its turning circle is too wide to reach its target; the steering now slows for tight turns (`RiderSteering.SpeedForTurn`, simulated over 30 cases: stuck in 23 before, 0 after). Unity EditMode tests 392 passed, 0 failed; build clean; the built app ran the sidecar for 3 minutes with no errors and did its tricks. Shareable build: `Builds/Share/Desktop Rider 2.1.zip` in the Unity project. Changed scripts copied to `Tools/Unity/DesktopRider3D-v2.1/`. Awaiting the owner's look.

3D app, version 2.2 (18 Nov 2026): the owner's video showed the sidecar crawling round near a trick's start, snapping to face the jump, and setting off at full speed from a stop. The rider now rides a proper run-up along the trick's line, arrives at the trick's own speed and facing the way it goes, and rides on after a trick that ends moving. Unity tests 400 passed, 0 failed; build clean; in the built app the sidecar lined up for and rode Double, Wheelie and NoHander, and Harvey Backflip and Whoops. Shareable build: `Builds/Share/Desktop Rider 2.2.zip`. Awaiting the owner's look.

3D app, version 2.3 (18 Nov 2026): the rider now plans the whole way to a trick and follows it at speed (no crawling), never turns on the spot as a trick starts (at most 4 degrees), and the corner pose no longer flicks out and snaps off. Unity tests 403 passed, 0 failed; build clean; checked in frame-by-frame recordings of the built app at normal and largest size. The owner's "big flashes" were not seen in those recordings (they record only the app's own picture); awaiting a screen recording from the owner if they remain. Shareable build: `Builds/Share/Desktop Rider 2.3.zip`.

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
| 4 | Sidecar 41 in the app (owner's request) | Built in 0.9.0. 21 of the approved sidecar motions rendered as sprites in both directions (`Tools/Blender/Sidecar/sprites.py`, three-quarter view 18° round and 8° down, 170 pixels per metre); the two corner moves are not used. Rider settings has a **Character** choice between Harvey and Sidecar 41, the menu and the conversation use the sidecar's own tricks (double, passenger superman, flying chair, power slide, rollers, lost passenger, passenger wave). Manifest, sheets, tricks and the settings choice are unit tested; the look awaits the owner's check (M11). |
| 4 | Riding about with a speed control (owner's request) | Built in 0.9.0. Riding off, riding along and riding back now move the window across the screen, for any character, at the character's real speed scaled to its size; it turns round at the edge of the screens. Rider settings has a **Riding speed** slider (quarter speed to double) with a Normal button. Speed rules and screen-edge steps are unit tested; the feel awaits the owner's check (M11). |
| 3 | Organise all supplied references | Done. Stored privately by the owner; folder structure and written summary are in `References/`. |

## Latest results

| Check | Result |
|---|---|
| Build (`dotnet build`, warnings as errors) | Succeeded, 0 warnings, 0 errors |
| Tests (`dotnet test`) | 668 passed, 0 failed, 0 skipped (Core 174, Animation 101, AI 393) |
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

## Fix 0.9.1 (17 Nov 2026)

The 0.9.0 Windows build left the larger sprite sheets (every jump and trick sheet, for Harvey and the sidecar) inside the single-file exe instead of the Assets folder, so the sidecar could not load and the app fell back to Harvey. The sprite content now has `ExcludeFromSingleFile` and `CopyToPublishDirectory`; a test (`PublishLayoutTests`) checks the project keeps them that way, and the published folder was checked to hold all 110 sprite files.

## Technical decisions

Recorded in ARCHITECTURE.md.

## Next task

Phase 8: Settings and polish (complete the settings interface, start-up options, transitions and visual polish, packaging). 

- Sidecar Move Viewer (17 Nov 2026): after new model files were imported the window kept using the old, destroyed outfit and its moves, so nothing played (MissingReferenceException every frame). It now looks the outfit up again whenever the project changes, a scene opens or the old one has gone, and loads jump props straight from the Props folder if the scene's list is out of date. Checked in the owner's Unity: compiles with no errors, Double renders mid-air.
- PassengerSuperman moved onto the big double with a longer hold (17 Nov 2026): fully out from 20% to 78% of the flight (about 0.6 s, was about 0.2 s). Bodycheck pas_in_bike 23, drv 84 (baseline). Checked in the owner's Unity.
- Big double landing moved further out (17 Nov 2026, owner: 'landing needs moving again'): the back wheel now first touches about 0.6 m past the top edge, on the start of the far slope, instead of about 2 m down it. Landing at 17.14 m (was 16.64), air 1.13 s. Affects Double and PassengerSuperman. Unity's shot command gained a 'side' view that places the move's ramps, for checking landings.
- APPROVED by the owner 17 Nov 2026 ('sweet lockem in'): Double (big double, landing just past the top edge) and PassengerSuperman (on the big double, long hold). Approved build snapshot: project files sidecar/approved/2026-11-17-jumps/ (Sidecar41.fbx, manifest, sidecar_anim.py). Do not change these two without asking.
- APPROVED by the owner 17 Nov 2026 ('they are all good'): every sidecar motion (27 clips). Snapshot: project files sidecar/approved/2026-11-17-all/.
