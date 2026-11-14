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
| Speech bubble | `SpeechBubbleViewModelTests` (AI) | Stepping aside during a trick and coming back afterwards with its full reading time, a reply that arrives during a trick, no bubble when there is nothing to say or it was dismissed, and the same while chatting in bubbles or with the conversation window open. Hidden at first, thinking then the growing reply then left to be read, staying away while the conversation window is open, taking over when the window closes mid-reply, problems, dismissing, clearing, long replies shortened, reading time, the wait restarting for each new message. |
| Tricks on request | `RiderActionsTests` (AI), `ConversationViewModelTests` (AI), `CharacterBehaviourTests` | Plain requests recognised and questions ignored, tags however written, tags split across pieces removed and other brackets kept, each action done once per message, nothing done when the AI cannot answer, the AI being told what it can do, a trick during thinking and talking, looping tricks ending properly, refusing unusable animations, and the real sprites having every animation that can be asked for. Each of the eighteen jumps, tricks and whoops by name, a longer name winning over a shorter one inside it ("nose wheelie" is a stoppie), everyday phrases that are not requests ("whoops, sorry"), "do a trick" picking one at random, and every tag being short enough to be hidden from the user. Kinds of action: which are tricks, things on the bike and gestures, and thinking and talking never being among them; "do some tricks" in seven wordings giving three or four different tricks and only tricks; talk about tricks that is not a request; the trick the rider names in its reply being done first; the `[trick]` and `[tricks]` tags; several tags in a row; the limit on actions for one reply; nothing done when the AI cannot answer; what the AI is told about each kind. Tags used as words: a trick's name taking the place of a tag in the middle of a sentence (also when split across pieces), a stray dash or colon after a leading tag dropped, and the AI being told to keep tags at the start and answer in a few easy words. Tricks in a row: played in the order asked with no resting frame between, during a reply, after a looping one, waiting for pictures that are not ready, leaving out one that cannot be shown, the limit on how many may wait, a menu choice dropping those waiting, and knowing when a trick is showing (from the first appearing to the last ending, including a rest between two). |
| The rider moving by itself | `ProactiveAnimationsTests` (AI) | Off by default with no timer over two days, something done now and then while the rider may start conversations and never the same twice running, the AI never contacted, the wait being random within its limits, how often following the user's choice, staying still while muted, hidden, in quiet hours or answering, switching off (or switching small movements off) removing the timer, an unrelated settings change not restarting the wait, the interface thread, and shutdown. |
| Personality | `RiderPersonalityTests` (AI), `AppSettingsTests` | The standard rider when no choices are made, each way of talking, short and detailed replies, the user's name, the user's own description replacing the built-in one, remembered things listed only when there are some, the rider always being told it cannot see the screen, and out-of-range choices being repaired. |
| Rider settings | `RiderSettingsViewModelTests` (AI) | Every choice in the settings window: what it shows at first, saving each choice once, radio buttons, names and descriptions, going back to the built-in rider, keeping conversations and for how long, deleting the conversation, adding and forgetting remembered things, switching memory off, the rider speaking first and how often, quiet hours and times typed in different ways, a failed save, and changes made elsewhere. |
| Local files | `LocalJsonFileTests` | Missing file, round trip, creating the folder, three kinds of damaged file and their backups, deleting, many writes at once. |
| Saved conversation | `ConversationHistoryTests` (AI) | Both sides of an exchange kept with their times, the user's message kept before the answer arrives, explanations never kept, last time's conversation coming back and being sent to the AI, lines older than the chosen time left out and dropped, nothing saved or read when switched off, clearing and switching off removing the saved copy, a failed save reported once, a stopped reply kept, and the real file: round trip, delete, damaged file, hand-edited file. |
| The rider's memory | `RiderMemoryTests` (AI) | Recognising "remember that…" in nine wordings and ignoring thirteen things that are not requests, keeping and saving a note, refusing empty, duplicate and too many notes, shortening long ones, memory switched off, loading, forgetting one and all, failed saves, the confirmation written into the conversation (and never sent to the AI or the speech bubble), the notes reaching the AI only while memory is on, and the real file. |
| The rider speaking first | `ProactiveCommentsTests` (AI) | Off by default with no timer and no request over two days, speaking only after the chosen time, no "thinking" shown, the remark in the speech bubble, stopping after three unanswered remarks until the user speaks, the wait restarting when the user speaks, mute and un-mute, quiet hours (including past midnight), a hidden rider, no model chosen, the AI unreachable (nothing shown), an empty remark, not interrupting an answer, the user's message taking precedence, switching on and off, how often, the menu lines, a failed save, the interface thread, and shutdown. |
| AI wiring | `CompositionTests` (AI) | AI services resolve with Core, the menu gains the AI lines, the conversation and its bubble resolve, the connection follows settings, the settings, saved conversation and memory resolve and write into the data folder, and the rider speaking first is off with no timer. |

Code coverage has not been measured. The requirement is that all applicable tests pass, and they do.

## 3D model checks (version 2.0)

The Unity models stay on the owner's PC, so these checks run there, in Blender, not on GitHub Actions:

```
blender --background --factory-startup --python Tools/Blender/verify_models.py -- <Unity project>/Assets/DesktopRider/Models <working .blend> <master .blend> <output folder> [bike_changed_vertices.json]
```

The script re-imports `DesktopRider.fbx` and the nine prop files into an empty Blender. It compares them against the working and master Blender files and prints one PASS or FAIL line per check:

- mesh vertex and face counts
- a UV map on each mesh, with no face that has area but no UV area
- bone names and parents
- the bike's shape identical to the master except the fork and chain vertices listed in the optional fifth argument, and only the listed vertices and faces removed and added, and the rider's identical except the helmet
- all 15 clips present, with the right lengths
- bone positions and rotations, and every deformed vertex of both meshes, within 1 mm and 0.5° of the Blender scene at 5 frames per clip
- the textures found
- each prop's size, triangles, UV map and materials

It exits with status 1 if any check fails.

Last run, 13 November 2026, on the owner's PC: **19 PASS, 0 FAIL**. The worst bone difference was 0.0009 mm, the worst deformed vertex difference 0.002 mm, and the worst rest-shape difference 0.0005 mm (rounding in the FBX format).

Known: 24 sliver faces on the rider's boot straps, each under 0.01 mm², have no UV area. They cannot be seen.

### Trick clips

```
blender --background --factory-startup --python Tools/Blender/verify_tricks.py -- <rigs-only DesktopRider_Tricks.fbx> <same export with meshes> <working .blend> Tools/Blender Right <output folder>
```

The rigs-only file has no meshes to compare, so the same export is also made with meshes into a scratch folder. The script keys the 18 tricks fresh in memory and samples every 4th frame of every clip (556 frames). For both files it checks the objects, bone names and hierarchy, all 18 clips and their lengths, and bone positions and rotations within 1 mm and 0.5°. For the file with meshes it also checks every skinned vertex within 1 mm. Last, it checks that both files carry the same animation curves. It exits with status 1 if any check fails.

Last run, 13 November 2026, on the owner's PC: **17 PASS, 0 FAIL**. Worst bone position 0.0026 mm, worst vertex 0.0032 mm, rotations 0.0000° (rotations up to 179° were in the samples, including the backflip).

After the fork tubes were extended (13 November, 22:21) `DesktopRider.fbx` was exported again and `verify_models.py` gave **19 PASS, 0 FAIL**: 64 bike vertices differ from the master, none outside the listed ones. `DesktopRider_Tricks.fbx` holds rigs only and no bone changed, so it was not exported again.

After the guard backs were deleted (22:49) the export was checked again: **19 PASS, 0 FAIL**, 8,520 vertices and 4,728 faces as expected, 212 vertices differ from the master, none outside the listed ones.

After the fork legs were remodelled and the chain closed (13 November, 22:41) `DesktopRider.fbx` was exported again: **19 PASS, 0 FAIL**. 232 bike vertices differ from the master, none outside the listed ones, with 112 vertices and 208 faces added as listed.

The wheel and brake-disc change of 13 November was a texture change only. The FBX files were not exported again, and `verify_models.py` was run again after the texture was replaced.

## Known limitations

- Development happens on a Linux build machine. It compiles the WPF application but cannot run it, so anything involving a real window is verified by GitHub Actions on Windows (build only) and by hand on the owner's Windows 11 PC.
- Loading the sprite pictures and drawing them (`RiderSprite`) needs a real window, so it is covered by manual procedure M3.
- The automated AI tests use a stand-in for Ollama (scripted answers, and one small local web server). They prove Desktop Rider handles everything Ollama can send, but only manual procedure M5 proves it against a real Ollama and a real model.
- `MessageWindow`, which shows AI progress and results, needs a real window, so it is covered by M5.
- The conversation window, the bubbles, the dark style and the code that puts them beside the rider (`ChatWindow`, `SpeechBubbleWindow`, `InputBubbleWindow`, `Themes/Dark.xaml`, `DarkMenuRenderer`, `ConversationPresenter`, `CompanionWindowPlacer`) need real windows, so they are covered by M6 and M7. Telling a click on the rider from a drag also needs a real mouse and is covered by M6. Everything they decide (what is said, when the bubble shows, where things go, which animation plays) is made by tested code.
- The settings window (`SettingsWindow`) and the **Keep the rider quiet** line of the rider's menu need a real window, so they are covered by M8. Everything they decide is made by tested code (`RiderSettingsViewModel`, `ProactiveComments`).
- What the rider actually says when it speaks first, and whether it uses a remembered fact well, depends on the AI model. The tests prove what is sent to the AI and when; only M8 shows what a real model makes of it.
- Reading a jump's large sheet in the background before a queued trick starts (`RiderSprite.PrepareTrick`) needs a real window and real pictures; it is covered by M10. The decisions around it (wait, start, skip) are tested.
- Whether a real AI model writes the tags it is told about cannot be tested automatically. The tests prove that "do some tricks" works whether or not it does.
- Passing "a trick is showing" from the rider's window to the speech bubble (`OverlayWindow.TrickShowingChanged`, `ConversationPresenter`) needs real windows and is covered by M10 step 13.
- The window that shows jumps and tricks (`StageWindow`), the swap between it and the rider's own picture, and reading large sheets in the background (`RiderSprite`) need real windows and are covered by M9. They have been compiled but never run by the developer.
- The jump, trick and whoop animations themselves were checked by eye from rendered frames by the developer. Whether they look right is the owner's judgement (M9).
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

### M8. Personality and memory (Phase 7, 0.7.0)

Needs Ollama running with a model chosen (M5).

1. Right-click the rider and choose **Rider settings**. **Expect:** a dark window titled "Rider settings" with three boxed sections: Personality, Memory, and Comments from the rider. It can be dragged by its heading and closed with the ✕ or Esc. The same window opens from **Rider settings** in the menu of the icon beside the clock.
2. Choose **Serious**, then ask the rider something. **Expect:** a plain answer with no jokes. Choose **Cheeky** again.
3. Choose **Longer, more detailed replies** and ask "how do I clean an air filter?". **Expect:** a noticeably longer answer than before. Choose short replies again.
4. Type your name under **What should the rider call you?**, click somewhere else, then say hello to the rider. **Expect:** the rider uses your name, at least some of the time.
5. Type "You are a grumpy old race mechanic with a heart of gold." in the description box and click somewhere else. **Expect:** the three "how should the rider talk" choices go grey, a **Go back to the built-in rider** button appears, and the rider's next answers are in that character. Press the button. **Expect:** the box empties and the choices come back.
6. Have a short conversation, exit Desktop Rider, start it again and click the rider. **Expect:** the conversation is still there, and the rider can answer a question about something said before the restart.
7. Press **Clear** in the conversation window, exit and start again. **Expect:** the conversation is empty.
8. In Rider settings, untick **Keep our conversation for next time**. Have a short conversation, exit and start again. **Expect:** the conversation is empty, and there is no `conversation.json` in `%LOCALAPPDATA%\DesktopRider`. Tick it again.
9. Say "remember that my bike is a 250 two-stroke" to the rider. **Expect:** a line from Desktop Rider in the conversation saying the rider will remember that, then the rider's answer. In Rider settings, "my bike is a 250 two-stroke" is listed under **What the rider remembers**.
10. Press **Clear**, then ask "what bike do I ride?". **Expect:** the rider knows.
11. Type something into the box under the list and press **Add** (or Enter). **Expect:** it joins the list and the box empties. Press **Forget** beside it. **Expect:** it goes.
12. Untick **Let the rider remember things about me** and ask "what bike do I ride?" in a fresh conversation. **Expect:** the rider does not know, and the list is still there. Tick it again.
13. Press **Forget everything**. **Expect:** the list empties and "The rider has forgotten everything." appears.
14. Leave Desktop Rider alone for an hour without touching **Let the rider start conversations**. **Expect:** the rider never speaks first.
15. Tick **Let the rider start conversations**, choose **About every 15 minutes**, close the conversation window and wait. **Expect:** after about 15 minutes a speech bubble appears with a short remark, with no "Thinking…" first, and the rider's mouth moves. It goes away by itself.
16. Do not answer. **Expect:** two more remarks at about 15-minute intervals, then silence until you next talk to the rider.
17. Right-click the rider and choose **Keep the rider quiet for an hour**. **Expect:** no remarks for an hour. The menu line now reads **Let the rider speak again**; choosing it lets the remarks resume.
18. Tick **Quiet hours**, set them to cover the present time, and wait past the next turn. **Expect:** no remark. Type "late" into one of the time boxes and click elsewhere. **Expect:** the box goes back to the time it had and a hint says what to type.
19. With **Let the rider start conversations** ticked, close Ollama and wait past the next turn. **Expect:** nothing appears at all: no remark and no error message.

Status: the owner ran the 0.7.0 build on 13 November 2026 and reported that it works great. The steps were not reported one by one. Built on Linux, so the settings window has never been seen by the developer.

### M9. Jumps, tricks and whoops (0.6.3, combined with Phase 7 in 0.7.1)

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

Status: the owner ran the 0.6.3 build on 13 November 2026 and reported that it works great. The steps were not reported one by one. Never run by the developer (Linux build machine).

### M10. Tricks in a row and the rider moving by itself (0.8.0, wording fixed in 0.8.1)

1. In the conversation, type "do some tricks". **Expect:** the rider answers and then does three or four different tricks one after another, with at most a brief rest between them. Every one is a jump, trick, wheelie, whoops or stoppie; never sleeping, looking round, revving or thinking.
2. Type "do some tricks" a few more times. **Expect:** a different selection each time.
3. Read the rider's answers. **Expect:** when it says which trick it is about to do ("how about a no-hander"), that trick is the first one it does.
4. Type "do a trick". **Expect:** exactly one trick.
5. Type "what tricks do you know?". **Expect:** an answer and no trick.
6. Type "do a backflip then a whip". **Expect:** both, one after the other.
7. While several tricks are queued, right-click the rider and choose something from **Do something**. **Expect:** that plays and the remaining queued tricks are dropped.
8. Watch between two jumps. **Expect:** the rider never freezes, and no ramp is left on screen.
9. With **Let the rider start conversations** off, leave Desktop Rider alone for 20 minutes. **Expect:** only the usual small idle movements; no tricks by itself.
10. Switch **Let the rider start conversations** on and leave it for 20 minutes. **Expect:** every few minutes the rider does something by itself: a trick, a rev, a look round. Ollama is not contacted for these.
11. Choose **Keep the rider quiet for an hour**. **Expect:** no tricks by itself and no remarks until un-muted.
12. Ask for tricks ten times in different words and read each answer. **Expect:** short answers such as "Sure, watch this!"; never a sentence with a word missing ("fancy a ?") and never an answer starting with a dash.
13. With the conversation window closed, ask for a backflip, then for some tricks. **Expect:** the speech bubble is not on screen while any trick plays, including between two tricks, and comes back when the last one lands, staying long enough to read. Choose a jump from **Do something** while a bubble is showing. **Expect:** the same.

Status: not yet run. Never run by the developer (Linux build machine).

---

# Version 2.0 (Unity rebuild): testing

Everything above this line describes version 1 (the Windows application under `src/`), which is kept unchanged.
Everything below describes the version 2.0 rebuild in Unity.

### 1. Logic tests

The logic (`unity/Assets/DesktopRider/Scripts/Logic/`) has **634 automated tests** in
`unity/Assets/DesktopRider/Tests/EditMode/`. They are NUnit tests and can be run two ways from the same files.

#### Without Unity (used for every change)

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

#### Inside Unity

Window > General > Test Runner > EditMode > Run All. The tests are in the assembly
`DesktopRider.Logic.Tests`. The project needs the packages `com.unity.test-framework` and
`com.unity.nuget.newtonsoft-json`.

### 2. Latest results

| Date | Where | Result |
|---|---|---|
| 13 November 2026 | `dotnet test`, .NET 10 on Linux | **634 passed, 0 failed, 0 skipped** (run four times in a row) |
| 13 November 2026 | `dotnet build`, both targets | 0 warnings, 0 errors |
| – | Unity editor Test Runner | **Not yet run.** See section 5. |

Before the move, version 1's own 620 tests were run from release 0.8.1 as the baseline: 620 passed.

### 3. What the tests cover

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

#### How the tests were converted from xUnit to NUnit

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

#### Test data

| Path | What it is |
|---|---|
| `Tests/EditMode/Data/sprites.json` | Version 1's real list of animations. Two tests check it contains every animation the conversation can ask for. |
| `Tests/EditMode/Data/Version1/*.json` | Settings, memory and conversation files **written by the real version 1 code** (release 0.8.1), with accented letters, quotes, an emoji and non-UK time offsets. |

### 4. Saved files work in both directions

- **Version 1's files read by version 2** – automated: `Version1FilesTests` loads the files above and checks
  every value, then saves them again and checks the names and shapes are unchanged.
- **Version 2's files read by version 1** – checked by hand on 13 November 2026. It is not automated because
  the Unity tests cannot load version 1's .NET 10 projects. The logic loaded the version 1 files, changed several values, switched free riding on,
  added a note and a line containing `— ï “ ” < > &` and an emoji, and saved. Version 1's own stores
  (release 0.8.1) then loaded all three files: status `Loaded`, every value correct, the added note and line
  intact with their time offsets, and no file set aside as damaged. **Passed.**

  To repeat it: write the files with the logic's `JsonSettingsStore`, `JsonMemoryStore` and
  `JsonConversationStore`, then load the same folder with version 1's classes of the same names.

One thing to know: version 1 does not know about free riding, so if version 1 saves the settings, the
`freeRiding` line is left out and the setting returns to off.

### 5. Known limitations

- **Run inside Unity 6000.6.6f1 on 13 November 2026: 650 passed, 0 failed** (the 634 logic tests plus 16 riding tests).
  The assembly definitions resolve `Newtonsoft.Json.dll` and `nunit.framework.dll`, and the runner accepts the
  `async` parameterised tests.
- **Mono differs from .NET 10 in one way that mattered.** The first Unity run failed 3 animation tests that pass on
  .NET 10: Mono computes `TimeSpan.TotalSeconds` as ticks × 1e-7, so 0.2 s became 0.19999… and the player showed the
  previous frame at exact frame boundaries. `AnimationPlayer` now divides ticks by `TimeSpan.TicksPerSecond`. The three
  existing tests are the regression tests; they only catch it when run in Unity, so always run the Unity suite.
- `OllamaProvider` is tested against a stand-in for the network, never a real Ollama. A manual check with
  Ollama running is needed once there is a conversation window in Unity.
- A handful of tests wait on real time (tens of milliseconds) for time limits and cancellation.

### 6. Unity .meta files

Unity identifies each file by an id in a `.meta` file beside it. `logic-tests/tools/make_unity_meta.py`
writes those for the logic and its tests, working each id out from the file's path so it is always the same.
Run it after adding, moving or deleting a file there. It never changes an existing id. The shared folders
above them (`Assets/DesktopRider`, `Scripts`, `Tests`) are left for Unity to create.

### 7. Running the Unity tests and the proof build

The Unity project is `unity/` in the repository (Assets, Packages, ProjectSettings) and lives on the development PC at
`D:\AI\Claude\Dektop Rider 3D\Desktop Rider 3D`. `EditorBridge` (Assets/DesktopRider/Editor) lets the editor be
driven without clicking: write one line `<id> <command> [argument]` to `Bridge/command.txt` at the project's top
level and the answer appears in `Bridge/result-<id>.txt`. Compiler results are appended to `Bridge/compile.txt`.

| Command | What it does |
|---|---|
| `t1 test` | Runs every EditMode test; the result file gives passed, failed and each failure |
| `r1 refresh` | Makes Unity pick up changed files |
| `s1 setup` | Sets up the open scene and player settings for the overlay proof |
| `b1 build` | Builds `Builds/Proof/DesktopRider3D.exe` |
| `u1 run` / `x1 stop` | Starts the proof, or asks it to close |
| `p1 snapshot 270 Idle 0.5` | Draws the rider from that direction (degrees), posed at that clip and second, to `Bridge/snapshot-270.png`, and lists its materials and textures. Used to check the model without seeing the screen |

The same steps are on the editor's **Desktop Rider** menu. In the Test Runner window (Window, General, Test Runner)
the EditMode tab runs them by hand.

**Riding tests** (`Tests/Riding`, 28 tests): headings, shortest turn, turn limit, braking so the bike stops on its
target and lean angle limits for `RiderSteering`; and for `DesktopView`, where ground points appear on the desktop,
the round trip back to the ground, aiming the small window's camera, and keeping the window on the desktop.

**Known flaky test:** `OllamaProviderTests.StreamChat_ALongReplyThatKeepsArriving_IsNotTimedOut` depends on timing
and failed once (of 662) while Unity was importing the textured model at the same time; it passed on the next run.
Unity total: 662 passed, 0 failed.

### 8. Overlay proof: manual check

Automated tests cannot see the desktop. With the proof running:

1. The bike rides about the main screen by itself with the desktop and windows visible behind it.
2. Clicks anywhere except on the bike reach the windows behind.
3. The bike can be picked up and dragged.
4. Ctrl+Shift+Q closes it. `Builds/Proof/proof-stats.txt` records frame rate, memory and processor use every 5 s.

Status on 13 November 2026: steps 1 and 3 confirmed by Boss on the PC. Step 2 still to confirm.

### 9. Desktop basics: manual check (phase 2)

With the built program running:

1. A rider icon appears beside the clock. Left click hides the rider; left click again brings it back.
2. Right click opens the menu: hide or show, always on top, move back to the middle, ride around freely, start with
   Windows, exit. Ticks match the current state.
3. "Ride around freely" off: the rider stays where it was put and can still be dragged. On: it rides about every screen.
4. With two screens, the rider can be dragged to and ride on both, never into a gap between them.
5. At 150% screen scaling the rider is drawn 1.5 times bigger than at 100%.
6. "Start with Windows" adds `Desktop Rider 3D` under HKCU\Software\Microsoft\Windows\CurrentVersion\Run, and
   unticking removes it.
7. Close and reopen: the rider is where it was left.

Status on 14 November 2026: not yet run (written with the PC off).

The Unity scripts can be compile-checked outside Unity by building them against the `UnityEngine.Modules` 2021.3.33
reference package with `UNITY_STANDALONE_WIN` defined. This catches most mistakes but is not a substitute for Unity 6.

### 10. Terrain shapes and the trick runner (ready for phase 4)

**Automated.**

- `python -m unittest discover -s Tools/Blender/tests` runs 27 tests of `Tools/Blender/terrain_shapes.py`. They need plain
  Python only, no Blender, and also run in CI. They check:
  - The take-off leaves at its face angle and height, its curve tightens as it rises, and its back slopes at the dirt angle.
  - The landing has a flat deck, a rounded knuckle, a face that steepens a little and a smooth run-out.
  - The tabletop parts come in order. The whoops vary, have a steeper back side and cupped tops.
  - The braking bumps grow, the wobble is small and repeatable, and the cross-section has sloped sides, a tyre groove and banking.
  - The jump distances match References/Terrain.
- The same command runs 32 more tests (59 in all) of the dirt itself. The texture ones need numpy, which Blender ships;
  without it they are skipped, not failed:
  - `prop_detail.py`: the mounds are closed solids, the riding line keeps the trick outline, sides are lumpy and
    slope out, and the wet and dry dirt boundary is ragged.
  - `dirt_texture.py`: sizes and ranges, real dark dirt colours, damp darkest and dry palest, no seams, normal maps of
    unit length that lean the right way over a bump.
  - `dirt_bake.py`: the wear weights add up to one, faces are damp, tops dry and sides loose, wet and dry fade into
    each other along an irregular line, and the baked images fit the prop.
- Manual: in the Unity editor, the bridge command `prop <name> [yaw] [pitch] [zoom]` renders one prop, lit by the scene,
  to `Bridge/prop-<name>.png`, for judging the dirt close up.
- Logic tests for `TrickLayout`, `TrickRun` and `TrickPicker` cover:
  - Reading props_layout.json and explaining a broken file.
  - Travel between frames.
  - Prop placement in the way the rider faces.
  - Speed, and finishing.
  - Turning as little as possible to keep a trick on screen, turning round at a wall, and giving up when nothing fits.
  - Never the same trick twice in a row.
- A `RidingArea.IsOnScreen` test covers the gaps between screens and the margin.

**Manual, in the editor** (after `setup`, with the props and tricks model imported, in play mode):

1. Send `trick` through the bridge. A random trick starts, and the answer lists the tricks available.
2. Send `trick Jump`. The ramp and landing appear ahead of the rider, the bike rides over them, and the ramp and
   landing disappear at the end. Nothing is cut off at the window's edge.
3. Put the rider near a screen edge, facing it, and send `trick Jump`. The trick turns away from the edge.
4. Pick the rider up mid-trick. The props disappear and the rider is carried as normal.
5. After a trick the rider rests, then carries on riding (free riding on) or stays parked (off).

Status on 14 November 2026: automated parts pass in the cloud. The Unity side is compile-checked outside Unity only.
The manual check is not yet run.
