# Desktop Rider

Desktop Rider is a Windows 11 desktop companion: an animated cartoon motocross rider and motorcycle that live on your desktop and can chat with you through an AI model running on your own computer (Ollama).

**Status: early development.** The application foundation is in place. The rider, animations and chat are still to come. See [Documentation/PROGRESS.md](Documentation/PROGRESS.md).

## Privacy

Desktop Rider keeps everything on your computer. There is no telemetry and nothing is uploaded. Settings and logs are stored in `%LOCALAPPDATA%\DesktopRider`.

## Building from source

You need Windows 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet build
dotnet test
dotnet run --project src/DesktopRider.App
```

Installation, Ollama setup and usage instructions will be added here as those features are built.

## Documentation

- [DESIGN.md](DESIGN.md): what is being built.
- [CLAUDE.md](CLAUDE.md): how it is being built.
- [Documentation/ARCHITECTURE.md](Documentation/ARCHITECTURE.md): how the code is organised.
- [Documentation/TESTING.md](Documentation/TESTING.md): how it is tested.
- [Documentation/PROGRESS.md](Documentation/PROGRESS.md): what is done and what is next.
