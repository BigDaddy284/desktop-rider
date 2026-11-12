namespace DesktopRider.Core.Infrastructure;

/// <summary>Makes sure only one copy of Desktop Rider runs per signed-in user, so two riders never appear at once.</summary>
public sealed class SingleInstanceGuard : IDisposable
{
    public const string DefaultName = "DesktopRider.SingleInstance";

    private readonly Mutex _mutex;
    private bool _disposed;

    public SingleInstanceGuard(string name = DefaultName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _mutex = new Mutex(initiallyOwned: false, name, out var createdNew);
        IsFirstInstance = createdNew;
    }

    /// <summary>True when no other copy was running when this one started.</summary>
    public bool IsFirstInstance { get; }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _mutex.Dispose();
        }
    }
}
