# Desktop Rider

Desktop Rider is a Windows 11 desktop companion: an animated cartoon motocross rider and motorcycle that live on your desktop and can chat with you through an AI model running on your own computer (Ollama).

**Status: early development.** The animated rider lives on the desktop, can be shown, hidden and closed from the icon beside the clock, and can talk with you through Ollama in a compact conversation window and speech bubbles. Memory between sessions and a settings screen are still to come.

## Using Desktop Rider

- Click the rider to talk to it. Click it again to put the conversation away.
- Drag the rider to move it. Right-click it for its menu.
- Desktop Rider also has an icon beside the clock (click the small arrow there if you cannot see it). Click the icon to hide the rider or bring it back. Right-click the icon for **Show the rider** / **Hide the rider**, **Always on top**, **Move back to the corner** and **Exit Desktop Rider**.
- While the rider is hidden, Desktop Rider keeps running quietly. Choose **Exit Desktop Rider** to close it completely.

## Connecting the rider to Ollama

Desktop Rider talks to [Ollama](https://ollama.com), a free program that runs AI models on your own computer. Nothing is sent to the internet.

1. Install Ollama and add at least one model to it (Ollama's own instructions explain how).
2. Right-click the Desktop Rider icon beside the clock and choose **Check the AI connection**. A small Desktop Rider window opens straight away and tells you whether Ollama was found and how many models it has.
3. Open the menu again and pick a model under **Choose an AI model**. Desktop Rider never picks one for you.
4. Click the rider and start typing. See "Talking to the rider" below.

If Ollama is not running, Desktop Rider says so and carries on as normal; the rider and its animations do not depend on it. Desktop Rider only contacts Ollama when you ask it to: when you choose one of these menu items, open the conversation window or send a message. The one exception is if you switch on **Let the rider start conversations** (off unless you turn it on); see "Rider settings" below.

Desktop Rider looks for Ollama at `http://localhost:11434`, which is where Ollama normally is. The rider's personality and memory are changed in **Rider settings** (see below). A screen for changing the Ollama address comes later; until then it can be changed in `%LOCALAPPDATA%\DesktopRider\settings.json`.

## Talking to the rider

- Click the rider (or choose **Talk to the rider** from either menu). A small conversation window opens beside it.
- Type a message and press Enter. The rider thinks, then answers word by word. Shift+Enter starts a new line.
- **Stop** ends a reply early. **Clear** starts a fresh conversation and deletes the saved copy of the old one.
- The line under the heading shows which AI model is answering and whether Ollama is connected. You can pick a different model from the list there.
- Close the window with Esc, the ✕, or another click on the rider. If the rider is still answering, the rest appears in a speech bubble above it; click the bubble to open the conversation again.
- If something goes wrong (for example Ollama is not running), the rider says so in plain words and carries on.

**Ask for a trick.** Ask the rider to do a wheelie, rev the engine, celebrate, have a nap, go for a ride or look left or right, and it does it while it answers.

**Jumps and freestyle tricks.** The rider can also hit a jump, throw a whip, do a backflip, superman, superman seat grab, no-hander, no-footer, nothing, heel clicker, can-can, nac-nac, lazy boy, one-hander, indian air or rock solid, scrub a jump, blitz a supercross whoop section, or do a stoppie. Ask for one by name ("do a backflip"), say "do a trick" to let it choose one, say "do some tricks" for three or four different ones in a row, or pick one from **Do something** on the rider's right-click menu. A ramp rolls in, the rider does the trick and lands, and everything clears away again. The AI can also choose one of these by itself when it suits the conversation. The speech bubble steps out of the way while a trick is on screen and comes back afterwards if there is something to read. When you ask for tricks you only ever get tricks, and if the rider says which trick it is about to do, that is the one it does.

**Chat in bubbles.** If you would rather not have a window, right-click the rider and tick **Chat in bubbles** (or press **Bubbles** in the conversation window). A click on the rider then opens a small typing bubble below it, like the message box in a messaging app, and the rider answers in its speech bubble. The small window button in the typing bubble switches back, and clicking the speech bubble opens the conversation window with everything said so far.

The conversation is still there the next time you start Desktop Rider, and the rider remembers what was said. You can switch that off in Rider settings.

## Rider settings

Right-click the rider, or the icon beside the clock, and choose **Rider settings**. Every choice is saved as you make it; there is no Save button.

**Personality**

- **How should the rider talk?** Cheeky (the standard rider), friendly, or serious.
- **How much should the rider say?** Short replies that fit a speech bubble, or longer and more detailed ones.
- **What should the rider call you?** Type your name, or leave it empty and the rider uses none.
- **Describe the rider in your own words.** Optional. Anything written here replaces the choice of how the rider talks. **Go back to the built-in rider** removes it.

**Memory**

- **Keep our conversation for next time.** On to begin with. Choose how long conversations are kept: a week, a month, three months or a year. Switching it off deletes the saved copy.
- **Delete the conversation** clears it from the screen and from your computer.
- **Let the rider remember things about me.** On to begin with. Say "remember that my bike is a 250" in a conversation, or type something into the box and press **Add**. The rider only remembers what you ask it to, and Desktop Rider confirms in the conversation when something has been remembered.
- Each remembered thing has a **Forget** button, and **Forget everything** deletes them all. Switching memory off stops the rider using what it remembers without deleting it.

**Comments from the rider**

- **Let the rider start conversations.** Off unless you turn it on. When on, the rider says something of its own now and then when you have not been talking: a remark about bikes, a tip, or an offer of help. It uses your AI model to think of it. While this is on the rider also does a trick or a small movement by itself every few minutes, without using the AI.
- Choose how often (about every 15 minutes, 30 minutes, hour or two hours) and, if you like, **quiet hours** when it never speaks first.
- To hush it for a while, right-click the rider and choose **Keep the rider quiet for an hour**. That also stops it doing tricks by itself.
- If you do not answer three remarks in a row, the rider stops until you next talk to it.

## Privacy

Desktop Rider keeps everything on your computer. There is no telemetry and nothing is uploaded. Settings, the saved conversation (`conversation.json`), what the rider remembers (`memory.json`) and logs are stored in `%LOCALAPPDATA%\DesktopRider`. The words of your conversations and what the rider remembers are never written to the logs. They are sent only to the Ollama address you use, as part of your own messages.

## Building from source

You need Windows 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
dotnet run --project src/DesktopRider.App
```

Installation instructions will be added here when the application is packaged.

## Documentation

- [DESIGN.md](DESIGN.md): what is being built.
- [CLAUDE.md](CLAUDE.md): how it is being built.
- [Documentation/ARCHITECTURE.md](Documentation/ARCHITECTURE.md): how the code is organised.
- [Documentation/TESTING.md](Documentation/TESTING.md): how it is tested.
- [Documentation/SPRITE_SPECIFICATION.md](Documentation/SPRITE_SPECIFICATION.md): how the animations are stored and played.
- [Documentation/PROGRESS.md](Documentation/PROGRESS.md): what is done and what is next.
