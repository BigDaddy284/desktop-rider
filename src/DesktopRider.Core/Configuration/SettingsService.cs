namespace DesktopRider.Core.Configuration;

/// <summary>Holds the settings in use while the application runs, and saves changes to them.</summary>
public interface ISettingsService
{
    /// <summary>The settings currently in use. Standard settings until <see cref="InitializeAsync"/> has run.</summary>
    AppSettings Current { get; }

    SettingsLoadStatus LoadStatus { get; }

    /// <summary>Raised after the settings have been changed and saved.</summary>
    event EventHandler? Changed;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies a change to the current settings, saves them and tells the rest of the application.</summary>
    Task UpdateAsync(Action<AppSettings> change, CancellationToken cancellationToken = default);
}

public sealed class SettingsService(ISettingsStore store) : ISettingsService
{
    private readonly ISettingsStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public event EventHandler? Changed;

    public AppSettings Current { get; private set; } = new();

    public SettingsLoadStatus LoadStatus { get; private set; } = SettingsLoadStatus.Defaults;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var result = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        Current = result.Settings;
        LoadStatus = result.Status;
    }

    public async Task UpdateAsync(Action<AppSettings> change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        change(Current);
        SettingsNormalizer.Normalize(Current);
        await _store.SaveAsync(Current, cancellationToken).ConfigureAwait(false);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
