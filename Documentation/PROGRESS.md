# Progress

Last updated: 13 November 2026

## Current task

Phases 1, 2 and 5 are complete. Phase 6 (conversation interface) is complete: built, tested automatically, and confirmed by the owner on Windows 11 with the 0.6.2 build, including the dark style, chat in bubbles and tricks on request. Next is Phase 7: personality and memory.

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
| 4 | Jumps, freestyle tricks, whoops and a stoppie (owner's request) | Built in 0.6.3: 18 new animations rendered from the owner's own models in both directions (jump, whip, scrub, backflip, superman, superman seat grab, no hander, no footer, nothing, heel clicker, can-can, nac-nac, lazy boy, one hander, indian air, rock solid, whoops, stoppie), larger frames with their own window so ramps and height fit, and all of them available on request and to the AI. Manifest, frame rules and requests are unit tested. How they look on Windows awaits the owner's check (M8), and the ramps, mounds and whoops are new artwork awaiting approval. |
| 3 | Organise all supplied references | Done. Stored privately by the owner; folder structure and written summary are in `References/`. |

## Latest results

| Check | Result |
|---|---|
| Build (`dotnet build`, warnings as errors) | Succeeded, 0 warnings, 0 errors |
| Tests (`dotnet test`) | 390 passed, 0 failed, 0 skipped (Core 142, Animation 76, AI 172) |
| GitHub Actions build and test on Windows | Passed for Phase 1; runs again on each push |
| Standalone Windows build (`dotnet publish`, win-x64) | Succeeded |
| Manual checks on Windows 11 | The owner ran the 0.3.0 test build: it starts, shows the animated rider and works. M1 to M3 not yet reported step by step. |

## Remaining tasks

- **Phase 4 – Animation system:** connect animations to behaviour (moving around the screen, reacting to the user) as those features arrive.
- **Phase 7 – Personality and memory.**
- **Phase 8 – Settings and polish.**
- **Phase 9 – Final validation.**

## Blockers

- None.

## Known issues

- The window that shows jumps and tricks larger than the rider's usual frame, and loading those large sheets in the background, have never been run by the developer (Linux build machine). They compile and the frame and origin rules are tested, but how they look on Windows depends on manual check M8.
- The new jumps and tricks were animated from descriptions of the techniques and simple flight physics, not traced frame by frame from film, and use a gentler gravity so a jump fits on a desktop. Timing and body positions may need adjusting after the owner has watched them.
- The conversation window, the speech bubble and click-versus-drag on the rider have never been run by the developer (Linux build machine). They compile and their logic is tested, but how they look and behave on Windows depends on manual check M6.
- The owner tried 0.6.0 and reported the conversation working, then asked for the dark Windows 11 style and a bubble chat mode before moving on; these are in 0.6.1. The dark style has never been seen by the developer, so its look is unconfirmed until the owner tries it.
- The owner tried 0.6.1 and reported it looks OK, but in bubble mode clicking the rider hid only the typing bubble and left the rider's answer up. 0.6.2 puts a finished answer away with the typing bubble (regression test in `SpeechBubbleViewModelTests`).
- Whether the AI itself chooses to do a trick unprompted depends on the model following an instruction, which small models may not. Plain requests such as "do a wheelie" do not depend on that.
- In the default corner the typing bubble only just fits below the rider. With Windows display scaling above 100% it will not fit there and appears beside or above the rider instead; dragging the rider up a little puts it below.
- The conversation is forgotten when Desktop Rider closes. Saving it is Phase 7.
- The owner tried the 0.5.0 build: choosing **Check the AI connection** appeared to do nothing. Results were shown as Windows notifications, which Windows can hide. 0.5.1 shows them in Desktop Rider's own small window instead, starting the moment the menu item is clicked. The owner confirmed 0.5.1 works.
- The developer can only run the AI code against a stand-in for Ollama. The owner has confirmed discovery, model choice and a streamed reply against a real Ollama; the failure and stop cases have only been tested automatically.
- The address, personality, temperature and waiting time have no settings screen yet (Phase 8); they can only be changed in the settings file.
- The window has never been run by the developer, because the build machine is Linux. Everything visual depends on manual checks M1 and M2.


- Multiple monitors have not been tried on real hardware. The owner uses a single 49-inch ultrawide monitor and has no second screen, so M2 steps 9 and 10 cannot be done there. The placement rules for several monitors are covered by automated tests only.

## Technical decisions

Recorded in ARCHITECTURE.md.

## Next task

Phase 7: Personality and memory (configurable personality, local conversation history, optional memory, proactive behaviour controls). 
