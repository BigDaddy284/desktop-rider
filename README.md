# Desktop Rider

Desktop Rider is a Windows 11 desktop companion: an animated cartoon motocross rider and motorcycle that live on your desktop and can chat with you through an AI model running on your own computer (Ollama).

**Status: early development.** The animated rider lives on the desktop and can be shown, hidden and closed from the icon beside the clock. The rider can connect to Ollama and answer a test hello; the speech bubbles and conversation window are next. See [Documentation/PROGRESS.md](Documentation/PROGRESS.md).

## Using Desktop Rider

- Drag the rider to move it. Right-click it for its menu.
- Desktop Rider also has an icon beside the clock (click the small arrow there if you cannot see it). Click the icon to hide the rider or bring it back. Right-click the icon for **Show the rider** / **Hide the rider**, **Always on top**, **Move back to the corner** and **Exit Desktop Rider**.
- While the rider is hidden, Desktop Rider keeps running quietly. Choose **Exit Desktop Rider** to close it completely.

## Connecting the rider to Ollama

Desktop Rider talks to [Ollama](https://ollama.com), a free program that runs AI models on your own computer. Nothing is sent to the internet.

1. Install Ollama and add at least one model to it (Ollama's own instructions explain how).
2. Right-click the Desktop Rider icon beside the clock and choose **Check the AI connection**. A message tells you whether Ollama was found and how many models it has.
3. Open the menu again and pick a model under **Choose an AI model**. Desktop Rider never picks one for you.
4. Choose **Ask the rider to say hello**. The rider's reply appears as a message beside the clock. The first reply can take a while because Ollama has to load the model. While it is working, the menu offers **Stop the rider's reply**.

If Ollama is not running, Desktop Rider says so and carries on as normal; the rider and its animations do not depend on it. Desktop Rider only contacts Ollama when you choose one of these menu items.

Desktop Rider looks for Ollama at `http://localhost:11434`, which is where Ollama normally is. A settings screen for changing the address, the rider's personality and other options comes later; until then they can be changed in `%LOCALAPPDATA%\DesktopRider\settings.json`.

## Privacy

Desktop Rider keeps everything on your computer. There is no telemetry and nothing is uploaded. Settings and logs are stored in `%LOCALAPPDATA%\DesktopRider`.

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
