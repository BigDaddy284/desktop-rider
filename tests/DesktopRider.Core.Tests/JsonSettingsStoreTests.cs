using DesktopRider.Core.Configuration;
using DesktopRider.Core.Infrastructure;

namespace DesktopRider.Core.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly AppPaths _paths;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
    {
        _paths = new AppPaths(Path.Combine(_temp.Path, "data"));
        _store = new JsonSettingsStore(_paths, timeProvider: new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
    }

    public void Dispose()
    {
        _store.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task Load_WithNoFile_ReturnsDefaultsWithoutCreatingAnything()
    {
        var result = await _store.LoadAsync();

        Assert.Equal(SettingsLoadStatus.Defaults, result.Status);
        Assert.Equal(AiSettings.DefaultOllamaAddress, result.Settings.Ai.OllamaAddress);
        Assert.False(File.Exists(_paths.SettingsFile));
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsEveryValue()
    {
        var settings = new AppSettings();
        settings.Appearance.Scale = 1.5;
        settings.Appearance.Opacity = 0.7;
        settings.Appearance.AlwaysOnTop = false;
        settings.Appearance.ClickThrough = true;
        settings.Appearance.Position = new SavedPosition { X = -320.5, Y = 48, Monitor = @"\\.\DISPLAY2" };
        settings.Behaviour.ProactiveComments = true;
        settings.Behaviour.ProactiveIntervalMinutes = 45;
        settings.Behaviour.AnimationFrequency = AnimationFrequency.Frequent;
        settings.Behaviour.QuietHoursEnabled = true;
        settings.Behaviour.QuietHoursStart = new TimeOnly(21, 30);
        settings.Ai.OllamaAddress = "http://192.168.1.20:11434";
        settings.Ai.Model = "some-model:latest";
        settings.Ai.SystemPrompt = "Be brief.";
        settings.Ai.Temperature = 0.3;
        settings.Memory.Enabled = false;
        settings.Memory.RetentionDays = 7;
        settings.Application.StartWithWindows = true;
        settings.Application.TrayHintShown = true;

        await _store.SaveAsync(settings);
        var result = await _store.LoadAsync();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        var loaded = result.Settings;
        Assert.Equal(1.5, loaded.Appearance.Scale);
        Assert.Equal(0.7, loaded.Appearance.Opacity);
        Assert.False(loaded.Appearance.AlwaysOnTop);
        Assert.True(loaded.Appearance.ClickThrough);
        Assert.Equal(-320.5, loaded.Appearance.Position!.X);
        Assert.Equal(48, loaded.Appearance.Position.Y);
        Assert.Equal(@"\\.\DISPLAY2", loaded.Appearance.Position.Monitor);
        Assert.True(loaded.Behaviour.ProactiveComments);
        Assert.Equal(45, loaded.Behaviour.ProactiveIntervalMinutes);
        Assert.Equal(AnimationFrequency.Frequent, loaded.Behaviour.AnimationFrequency);
        Assert.True(loaded.Behaviour.QuietHoursEnabled);
        Assert.Equal(new TimeOnly(21, 30), loaded.Behaviour.QuietHoursStart);
        Assert.Equal("http://192.168.1.20:11434", loaded.Ai.OllamaAddress);
        Assert.Equal("some-model:latest", loaded.Ai.Model);
        Assert.Equal("Be brief.", loaded.Ai.SystemPrompt);
        Assert.Equal(0.3, loaded.Ai.Temperature);
        Assert.False(loaded.Memory.Enabled);
        Assert.Equal(7, loaded.Memory.RetentionDays);
        Assert.True(loaded.Application.StartWithWindows);
        Assert.True(loaded.Application.TrayHintShown);
    }

    [Fact]
    public async Task Save_CreatesTheDataFolder_AndLeavesNoTemporaryFile()
    {
        await _store.SaveAsync(new AppSettings());

        Assert.True(File.Exists(_paths.SettingsFile));
        Assert.Equal(["settings.json"], Directory.GetFiles(_paths.DataDirectory).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Save_WritesReadableJson()
    {
        var settings = new AppSettings();
        settings.Behaviour.AnimationFrequency = AnimationFrequency.Rare;

        await _store.SaveAsync(settings);
        var json = await File.ReadAllTextAsync(_paths.SettingsFile);

        Assert.Contains("\"ollamaAddress\": \"http://localhost:11434\"", json, StringComparison.Ordinal);
        Assert.Contains("\"animationFrequency\": \"rare\"", json, StringComparison.Ordinal);
        Assert.Contains(Environment.NewLine, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_NormalizesValuesBeforeWriting()
    {
        var settings = new AppSettings();
        settings.Appearance.Scale = 500;

        await _store.SaveAsync(settings);

        Assert.Equal(AppearanceSettings.MaxScale, (await _store.LoadAsync()).Settings.Appearance.Scale);
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[1, 2, 3]")]
    public async Task Load_WithDamagedFile_UsesDefaultsAndKeepsABackup(string contents)
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFile, contents);

        var result = await _store.LoadAsync();

        Assert.Equal(SettingsLoadStatus.RecoveredFromDamagedFile, result.Status);
        Assert.Equal(1.0, result.Settings.Appearance.Scale);
        Assert.False(File.Exists(_paths.SettingsFile));
        Assert.Equal(Path.Combine(_paths.DataDirectory, "settings.damaged-20260102-030405.json"), result.BackupFile);
        Assert.Equal(contents, await File.ReadAllTextAsync(result.BackupFile!));
    }

    [Fact]
    public async Task Load_FillsInValuesMissingFromAnOlderFile()
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(_paths.SettingsFile, """{ "appearance": { "scale": 1.2 } }""");

        var result = await _store.LoadAsync();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(1.2, result.Settings.Appearance.Scale);
        Assert.Equal(AiSettings.DefaultOllamaAddress, result.Settings.Ai.OllamaAddress);
        Assert.True(result.Settings.Appearance.AlwaysOnTop);
    }

    [Fact]
    public async Task Load_ToleratesCommentsUnknownValuesAndBadRanges()
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        await File.WriteAllTextAsync(
            _paths.SettingsFile,
            """
            {
              // edited by hand
              "somethingFromTheFuture": true,
              "ai": { "temperature": 99, "ollamaAddress": "nonsense", },
            }
            """);

        var result = await _store.LoadAsync();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.Equal(AiSettings.MaxTemperature, result.Settings.Ai.Temperature);
        Assert.Equal(AiSettings.DefaultOllamaAddress, result.Settings.Ai.OllamaAddress);
    }

    [Fact]
    public async Task Save_OverwritesThePreviousFile()
    {
        var settings = new AppSettings();
        await _store.SaveAsync(settings);
        settings.Ai.Model = "second";
        await _store.SaveAsync(settings);

        Assert.Equal("second", (await _store.LoadAsync()).Settings.Ai.Model);
    }

    [Fact]
    public async Task ConcurrentSaves_NeverCorruptTheFile()
    {
        var saves = Enumerable.Range(0, 25).Select(i =>
        {
            var settings = new AppSettings();
            settings.Ai.Model = $"model-{i}";
            return _store.SaveAsync(settings);
        });

        await Task.WhenAll(saves);
        var result = await _store.LoadAsync();

        Assert.Equal(SettingsLoadStatus.Loaded, result.Status);
        Assert.StartsWith("model-", result.Settings.Ai.Model, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Save_HonoursCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.SaveAsync(new AppSettings(), cancellation.Token));
        Assert.False(File.Exists(_paths.SettingsFile));
    }

    [Fact]
    public async Task Save_RejectsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SaveAsync(null!));
    }
}
