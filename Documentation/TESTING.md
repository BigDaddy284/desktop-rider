# Testing

## Commands

Run from the repository root.

| Purpose | Command |
|---|---|
| Build everything (warnings are errors, analyzers on) | `dotnet build` |
| Run all automated tests | `dotnet test` |
| Run one test class | `dotnet test --filter FullyQualifiedName~JsonSettingsStoreTests` |
| Make a standalone Windows build | `dotnet publish src/DesktopRider.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish` |

Every push and pull request is also built and tested on Windows by GitHub Actions (`.github/workflows/ci.yml`).

## What is covered automatically

| Area | Test class | Covers |
|---|---|---|
| Settings defaults and repair | `AppSettingsTests` | Defaults match DESIGN.md; missing sections, out-of-range numbers, bad addresses and unusable positions are repaired. |
| Settings file | `JsonSettingsStoreTests` | Round trip of every value, missing file, damaged file and its backup, hand-edited files, overwrite, simultaneous saves, cancellation. |
| File locations | `AppPathsTests` | Paths stay inside the data folder; folders are created; version is available. |
| Logging | `FileLoggerProviderTests` | Line format, exception details, level filtering, a new file each day, old-file clean-up, many threads at once, an unusable folder, use after shutdown. |
| Character placement | `PlacementCalculatorTests` | Default corner, restoring a saved spot, negative-coordinate monitors, unplugged monitors, resolution changes, nudging back on screen, bad saved numbers. |
| Settings in use | `SettingsServiceTests` | Changes are applied, saved, kept in range and announced; nothing is announced when a save fails. |
| Character window logic | `OverlayViewModelTests` | The chat-in-bubbles switch, always-on-top toggle, showing and hiding the rider, menu actions, behaviour when a save fails. |
| Notification-area icon logic | `TrayViewModelTests` | Lines added by other parts (the AI), the menu while the rider is showing and while hidden, show, hide and exit, the tick on Always on top, what a click does, the hover text, and the reminder (once ever on first hide, every time on a hidden start, never twice). |
| Icon picture layout | `IconLayoutTests` | Finding the drawn part of a frame and fitting it into a square without distortion. |
| Sprite list | `SpriteManifestTests` | Reading the list, frame positions on a sheet, sixteen kinds of broken list, and the real rider list covering every animation DESIGN.md requires. Larger frames for jumps: their size, their place on a sheet, where the standard frame sits inside them facing either way, and that the real jump and trick animations are one-shots with room above and in front that line up on a whole pixel. |
| Speech bubble and conversation window placement | `PlacementCalculatorTests` | Above the rider by preference, then left, right and below; always fully on the rider's own screen; the gap; no screens at all. |
| Conversation animations | `CharacterBehaviourTests` | Thinking repeats without settling, thinking then talking then resting, a very quick reply still seen being spoken, a failed reply going straight back to rest, a new question interrupting, tricks still working, waking a sleeping rider first, characters without these animations, and the real sprites having them. |
| Animation playback | `AnimationPlayerTests` | Loops, one-shots, returning to the right loop, waiting requests, priorities, lead-in and lead-out animations, speed, long pauses. |
| Idle animations | `IdleVariationPickerTests` | Timing, switched off, not interrupting other animations. |
| Wiring | `CompositionTests` | Services resolve and work together; single-instance guard. |
| Ollama provider | `OllamaProviderTests` (AI) | Listing models, the request sent for a chat, pieces arriving one at a time, Ollama not running, model not installed, errors before and during a reply, replies that end early, broken connections, answers that are not from Ollama, time limits before and during a reply, a long reply that keeps arriving, stopping by the caller, and a full round trip over a real local network port. |
| Chat service | `AiChatServiceTests` (AI) | No model chosen, the user's model, temperature and personality being applied, unknown provider, connection reports, stopping a reply, shutdown during a reply, choosing a model. |
| AI menu lines | `AiMenuViewModelTests` (AI) | Nothing contacted until asked, the talk line, a message the moment a check starts, every connection-check message, choosing a model and its tick, a failed save, use after shutdown. |
| Conversation | `ConversationViewModelTests` (AI) | Nothing contacted until asked, sending and the reply growing in place, thinking then talking then idle, empty and over-long messages, earlier lines carried into later messages, problems explained but never sent to the AI, no model chosen, model gone, a reply with no words, stopping, clearing mid-reply, shutdown mid-reply, the limits on lines sent and kept, the connection check and its advice, choosing a model and a failed save. |
| Speech bubble | `SpeechBubbleViewModelTests` (AI) | Hidden at first, thinking then the growing reply then left to be read, staying away while the conversation window is open, taking over when the window closes mid-reply, problems, dismissing, clearing, long replies shortened, reading time, the wait restarting for each new message. |
| Tricks on request | `RiderActionsTests` (AI), `ConversationViewModelTests` (AI), `CharacterBehaviourTests` | Plain requests recognised and questions ignored, tags however written, tags split across pieces removed and other brackets kept, each action done once per message, nothing done when the AI cannot answer, the AI being told what it can do, a trick during thinking and talking, looping tricks ending properly, refusing unusable animations, and the real sprites having every animation that can be asked for. Each of the eighteen jumps, tricks and whoops by name, a longer name winning over a shorter one inside it ("nose wheelie" is a stoppie), everyday phrases that are not requests ("whoops, sorry"), "do a trick" picking one at random, and every tag being short enough to be hidden from the user. |
| AI wiring | `CompositionTests` (AI) | AI services resolve with Core, the menu gains the AI lines, the conversation and its bubble resolve, the connection follows settings. |

Code coverage has not been measured. The requirement is that all applicable tests pass, and they do.

## Known limitations

- Development happens on a Linux build machine. It compiles the WPF application but cannot run it, so anything involving a real window is verified by GitHub Actions on Windows (build only) and by hand on the owner's Windows 11 PC.
- Loading the sprite pictures and drawing them (`RiderSprite`) needs a real window, so it is covered by manual procedure M3.
- The automated AI tests use a stand-in for Ollama (scripted answers, and one small local web server). They prove Desktop Rider handles everything Ollama can send, but only manual procedure M5 proves it against a real Ollama and a real model.
- `MessageWindow`, which shows AI progress and results, needs a real window, so it is covered by M5.
- The conversation window, the bubbles, the dark style and the code that puts them beside the rider (`ChatWindow`, `SpeechBubbleWindow`, `InputBubbleWindow`, `Themes/Dark.xaml`, `DarkMenuRenderer`, `ConversationPresenter`, `CompanionWindowPlacer`) need real windows, so they are covered by M6 and M7. Telling a click on the rider from a drag also needs a real mouse and is covered by M6. Everything they decide (what is said, when the bubble shows, where things go, which animation plays) is made by tested code.
- The window that shows jumps and tricks (`StageWindow`), the swap between it and the rider's own picture, and reading large sheets in the background (`RiderSprite`) need real windows and are covered by M8. They have been compiled but never run by the developer.
- The jump, trick and whoop animations themselves were checked by eye from rendered frames by the developer. Whether they look right is the owner's judgement (M8).
- There are no automated tests of the WPF window itself. Its decisions (where to sit, what the menu does) are made by tested code in Core; the thin window layer that carries them out (`OverlayWindow`, `WindowPlacementController`, `TrayIconController`, `TrayIconFactory`) is covered by the manual procedures below.

- Multiple monitors (M2 steps 9 and 10) cannot be checked by hand: the owner has one ultrawide monitor. `PlacementCalculatorTests` covers those rules automatically.

## Manual test procedures

### M1. Start and stop (Phase 1)

1. Double-click `DesktopRider.exe`. **Expect:** the stand-in card appears near the bottom-right corner of the main screen.
2. With it still running, double-click `DesktopRider.exe` again. **Expect:** a message saying Desktop Rider is already running; no second card.
3. Right-click the card and choose **Exit Desktop Rider**. **Expect:** it disappears and `DesktopRider.exe` is gone from Task Manager.
4. Open `%LOCALAPPDATA%\DesktopRider\Logs`. **Expect:** a log file for today with a "starting" line and a "closing with exit code 0" line.

Status: **awaiting the owner's check on Windows 11.**

### M2. Overlay window (Phase 2: transparency, always-on-top, dragging, positioning)

1. Look at the card. **Expect:** no window frame, title bar or background rectangle; the desktop shows around its rounded corners. No taskbar button.
2. Click on the desktop or a window just outside the card's edge. **Expect:** the click goes to whatever is behind; the empty margin does not block it.
3. Open another application and drag it over the card. **Expect:** the card stays on top.
4. Right-click the card and choose **Always on top** to untick it, then click another window that overlaps it. **Expect:** the card now goes behind. Tick it again. **Expect:** it comes back on top.
5. Drag the card somewhere else. **Expect:** it follows the mouse smoothly.
6. Drag it so it hangs partly off the edge of the screen and let go. **Expect:** it hops back fully onto the screen.
7. Exit and start Desktop Rider again. **Expect:** the card appears where it was left, with the always-on-top choice remembered.
8. Right-click and choose **Move back to the corner**. **Expect:** it returns to the bottom-right corner of the main screen.
9. If a second monitor is available: drag the card onto it, exit, restart. **Expect:** it reappears on the second monitor. Then exit, unplug that monitor and restart. **Expect:** it appears in the corner of the main screen.
10. If the monitors use different scaling: drag the card between them. **Expect:** it stays sharp and a sensible size on both.

Status: **awaiting the owner's check on Windows 11.**

### M3. The animated rider (Phases 3 and 4)

1. Start Desktop Rider. **Expect:** the rider on the bike appears, facing right, gently moving. No grey card, no background rectangle.
2. Click just above the seat or between the wheels, where there is only desktop showing. **Expect:** the click goes to whatever is behind.
3. Watch for half a minute. **Expect:** now and then the rider looks around, then settles again.
4. Right-click the rider, choose **Do something**, then **Wheelie**. **Expect:** the front wheel lifts and comes back down without the picture jumping.
5. Try **Thinking**, **Celebration**, **Revving** and **Talking**. **Expect:** each plays smoothly; choosing **Idle** stops the looping ones.
6. Choose **Sleeping**. **Expect:** the rider slumps forward and stays asleep. Choose **Idle**. **Expect:** the rider sits back up before returning to rest.
7. Choose **Riding loop**. **Expect:** the bike pulls away on the spot with the wheels spinning. Choose **Idle**. **Expect:** it brakes and settles.
8. Choose **Turn around**. **Expect:** the bike faces left, the number 28 still reads the right way round, and the left side is the pink one.
9. Drag the rider while it is animating. **Expect:** it moves as before and keeps animating.
10. Open Task Manager and watch Desktop Rider for a minute while it rests. **Expect:** processor use stays low (a few percent at most).

Status: the owner ran the 0.3.0 test build on 12 November 2026 and reported that it looks and works well. The steps were not reported one by one, so steps 2, 9 and 10 (click-through, dragging while animating, processor use) are still to be confirmed individually.

### M4. Icon beside the clock (Phase 2: system tray controls)

1. Start Desktop Rider and look beside the clock (click the small arrow if the icon is not in view). **Expect:** a Desktop Rider icon that is a tiny picture of the rider and bike. Resting the mouse on it shows "Desktop Rider".
2. Right-click the icon. **Expect:** a menu with **Hide the rider**, **Always on top** (ticked), **Move back to the corner** and **Exit Desktop Rider**.
3. Choose **Hide the rider**. **Expect:** the rider disappears. The first time ever, a short message says the rider is hidden and Desktop Rider is still running.
4. Rest the mouse on the icon. **Expect:** "Desktop Rider (hidden)". Right-click it. **Expect:** the first item now reads **Show the rider**.
5. Choose **Show the rider**. **Expect:** the rider comes back in the same place, still animating.
6. Click the icon once with the left button. **Expect:** the rider hides. Click again. **Expect:** it comes back. No message this time.
7. Right-click the rider itself and choose **Hide the rider**. **Expect:** it hides, the same as from the icon.
8. With the rider hidden, open Task Manager and watch Desktop Rider for a minute. **Expect:** processor use at or very near 0%.
9. With the rider hidden, right-click the icon and choose **Exit Desktop Rider**. **Expect:** the icon disappears straight away and `DesktopRider.exe` is gone from Task Manager.
10. Start Desktop Rider again. **Expect:** the rider stays hidden as it was left, the icon is there, and the message appears to say it is running. Clicking the message or the icon brings the rider back.
11. With Desktop Rider running, double-click `DesktopRider.exe` again. **Expect:** a message that it is already running, which explains how to bring the rider back from the icon.
12. Untick **Always on top** from the icon's menu. **Expect:** the same effect as from the rider's own menu, and the tick matches in both menus.

Status: the owner ran the 0.4.0 build on Windows 11 on 12 November 2026 and reported that it all looks and works well. The steps were not reported one by one.

### M5. Connecting to Ollama (Phase 5)

Needs Ollama installed with at least one model. Every message in this procedure appears in Desktop Rider's own small window, which can be left open or closed with OK at any time.

1. Close Ollama completely (right-click its icon beside the clock and quit). Right-click the Desktop Rider icon and choose **Check the AI connection**. **Expect:** a small Desktop Rider window opens at once in the middle of the screen saying "Checking the AI connection"; within a few seconds it changes to "The AI is not connected", explaining that Ollama cannot be reached at `http://localhost:11434`. The rider keeps animating throughout.
2. Start Ollama. Choose **Check the AI connection** again. **Expect:** "Ollama is connected" with the number of models found.
3. Open the icon's menu. **Expect:** a new line **Choose an AI model** that opens a list of your models, none ticked.
4. Pick a model. **Expect:** a message "AI model chosen" that says how to start talking. Open the menu again. **Expect:** that model is ticked.
5. With Desktop Rider running and nothing chosen from the menu, watch Task Manager for a minute. **Expect:** processor use as low as before; nothing contacts Ollama on its own.

Until 0.5.1 this procedure also had steps for **Ask the rider to say hello**, a stand-in for the conversation window. From 0.6.0 that line is replaced by **Talk to the rider**, and replies, stopping and exiting mid-reply are checked in M6.

Status: the owner ran the 0.5.1 build on Windows 11 against their own Ollama on 12 November 2026 and reported that it works: the models were found and the rider replied. The steps were not reported one by one, so the Ollama-not-running, stop and exit-during-reply steps (1, 6, 7, 9) are still to be confirmed individually. With the earlier 0.5.0 build, which used Windows notifications for these messages, the check appeared to do nothing; 0.5.1 replaced the notifications with the window described above.

### M6. Talking to the rider (Phase 6: speech bubbles, conversation window, animations)

Needs Ollama running with a model chosen (M5).

1. Click the rider once without moving the mouse. **Expect:** a compact window headed "Talk to the rider" opens just above the rider, with the typing cursor already in the message box. The line under the heading shows your AI model and, after a moment, a green dot and "Connected".
2. Drag the rider somewhere else. **Expect:** the rider moves as before, the conversation window follows it, and letting go does not close the window. Drag the rider to the top edge of the screen. **Expect:** the window moves to the rider's side and stays fully on the screen.
3. Type a greeting and press Enter. **Expect:** your message appears on the right in a dark balloon; "The rider is thinking…" shows with a moving bar and the rider plays its thinking movement; then the reply appears on the left and grows word by word while the rider plays its talking movement; then the rider settles back to rest. The rider keeps animating smoothly throughout.
4. Ask a follow-up that only makes sense with the first message (for example "say that again in French"). **Expect:** the reply shows the rider remembered the conversation.
5. Ask for something long (for example "tell me a long story") and press **Stop** while it is arriving. **Expect:** the reply stops, what had arrived stays, and a yellow line says "Reply stopped."
6. Ask something, then press Esc (or the ✕) while the rider is still thinking. **Expect:** the window closes; a speech bubble above the rider says "Thinking…", then shows the reply growing; a few seconds after the reply finishes the bubble goes away by itself. It never takes the keyboard away from another program you are typing in.
7. Ask something else the same way and click the bubble while it is showing. **Expect:** the conversation window opens with the whole conversation in it, and the bubble goes.
8. Click the rider while the window is open. **Expect:** the window closes. Click again. **Expect:** it opens with the conversation still there.
9. Press **Clear**. **Expect:** the conversation empties and a hint about typing a message shows.
10. Use the list beside "AI model:" to pick a different model, then send a message. **Expect:** the reply comes from the new model, and the icon's menu shows the new model ticked.
11. Quit Ollama, then send a message. **Expect:** a yellow line explains in plain words that Ollama cannot be reached, the dot turns red with "Not connected", and the rider goes back to rest. Start Ollama, press **Check again** (close and reopen the window if the button is not showing). **Expect:** "Connected" again, and the next message is answered.
12. Right-click the rider and choose **Talk to the rider**; hide the rider and choose **Talk to the rider** from the icon's menu beside the clock. **Expect:** both open the conversation window; the second brings the rider back first.
13. Hide the rider while the window is open. **Expect:** the window goes with it.
14. Send a message and, while the reply is arriving, choose **Exit Desktop Rider**. **Expect:** it closes straight away and `DesktopRider.exe` is gone from Task Manager.
15. Open another program and type in it while the rider is resting with the conversation window closed. **Expect:** Desktop Rider never takes the keyboard, and Task Manager shows processor use as low as before.

Status: the owner ran the 0.6.0 build on 12 November 2026 and reported that it works. The steps were not reported one by one. The window's style changed in 0.6.1 (dark, with the model list as a menu), so its look is to be checked again with M7.

### M7. Dark style and chat in bubbles (0.6.1)

1. Right-click the rider. **Expect:** a dark menu with rounded corners, white words and a small picture beside each line, like Windows 11's own right-click menu. The line under the mouse is highlighted with a rounded shape. **Always on top** shows a tick instead of its picture.
2. Point at **Do something**. **Expect:** the list of animations opens beside it in the same style.
3. Right-click the Desktop Rider icon beside the clock. **Expect:** the same dark colours, with white ticks and arrows.
4. Click the rider. **Expect:** the conversation window is dark with rounded corners; your messages and the rider's are in grey balloons; the message box is a rounded bar with a round send button.
5. Click **AI model** in the window. **Expect:** your models open as a dark menu with the current one ticked; picking one changes the model.
6. Send a message with the window closed (send, then Esc). **Expect:** the speech bubble is dark with white words.
7. Right-click the rider and tick **Chat in bubbles**, then click the rider. **Expect:** instead of the window, a small dark typing bubble appears just below the rider with the cursor in it. (If the rider is right at the bottom of the screen with no room below, the bubble goes beside or above it.)
8. Type a message and press Enter. **Expect:** the typing bubble empties, the rider thinks and then answers in the speech bubble above it. The answer stays up while the typing bubble is open.
9. Send another message. **Expect:** the new answer replaces the old one, and the rider remembers the earlier message.
10. Click the rider (or press Esc). **Expect:** the typing bubble and the rider's answer both go at once. If the rider was still answering, the answer carries on and fades a few seconds after it finishes.
11. Click the rider to bring the typing bubble back, then click the small window button in it. **Expect:** the conversation window opens with everything said so far, and **Chat in bubbles** is no longer ticked. Press **Bubbles** in the window. **Expect:** back to the typing bubble.
12. In bubble mode, click the speech bubble while it shows a reply. **Expect:** the conversation window opens with the whole conversation.
13. Exit and start Desktop Rider again, then click the rider. **Expect:** the choice of bubbles or window was remembered.
14. Ask the rider "do a wheelie". **Expect:** as the answer starts, the rider does a wheelie, then carries on talking. No square-bracket tag such as [wheelie] is ever shown in the answer.
15. Ask "rev it" and then "go to sleep". **Expect:** the rider revs for a few seconds; then slumps asleep for a few seconds and sits back up by itself.
16. Ask "what is a wheelie?". **Expect:** an answer in words. The rider may or may not do one, depending on the AI model; either is fine.

Status: the owner ran the 0.6.1 build on 13 November 2026 and reported that it looks OK, with one problem: in bubble mode, clicking the rider hid the typing bubble but left the rider's answer showing. Fixed in 0.6.2 (step 10). The owner ran the 0.6.2 build on 13 November 2026 and reported that it is working well, including the fix and tricks on request. The steps were not reported one by one.

### M8. Jumps, tricks and whoops (0.6.3)

1. Right-click the rider, choose **Do something**, then **Jump**. **Expect:** after a short pause at most, the rider pulls away on the spot, a dirt jump rolls in from the front, the bike rides up it, flies, lands on a dirt slope and brakes to a stop as the slope rolls away behind. The rider ends exactly where it started.
2. Watch the start and the end closely. **Expect:** no flicker, and the rider does not shift even slightly when the jump begins or ends.
3. While a jump is playing, click on the empty area around the rider and on the ramp. **Expect:** clicks go to whatever is behind.
4. Try each of **Whip**, **Backflip**, **Superman**, **Superman seat grab**, **No hander**, **No footer**, **Nothing**, **Heel clicker**, **Can can**, **Nac nac**, **Lazy boy**, **One hander**, **Indian air**, **Rock solid** and **Scrub**. **Expect:** each plays smoothly from the steel ramp (the whip and the scrub from a dirt jump) and looks like the trick it is named after.
5. Choose **Whoops**. **Expect:** a row of dirt bumps rolls under the bike, which skims across the tops with the suspension working and the rider standing.
6. Choose **Stoppie**. **Expect:** the bike pulls away, brakes hard, the rear wheel lifts and is set back down.
7. Choose **Turn around**, then **Superman**. **Expect:** the same trick facing left, with the ramp arriving from the left and the number 28 the right way round.
8. Drag the rider to the top of the screen and choose **Backflip**. **Expect:** the part of the jump that goes above the screen is simply cut off; nothing else goes wrong.
9. Drag the rider while a trick is playing. **Expect:** the trick moves with it.
10. Hide the rider from the icon beside the clock while a trick is playing, then show it. **Expect:** no ramp or rider is left behind on the screen while hidden.
11. In the conversation, type "do a backflip". **Expect:** the rider does one as it answers. Type "do a trick". **Expect:** the rider picks a jump or trick itself.
12. Watch Task Manager while playing several tricks one after another. **Expect:** memory rises during a trick, comes back down afterwards and does not keep climbing from one trick to the next.

Status: **awaiting the owner's check on Windows 11.** Never run by the developer (Linux build machine).
