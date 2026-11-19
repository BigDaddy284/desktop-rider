#nullable enable

using System.Threading.Tasks;

// In the namespace that encloses every test, so the tests can use the short name on any version of .NET.
namespace DesktopRider
{
    /// <summary>
    /// A signal a test can wait for and set. Newer versions of .NET have this built in; .NET Standard 2.1,
    /// which Unity uses, only has the kind that carries a value.
    /// </summary>
    internal sealed class TaskCompletionSource
    {
        private readonly TaskCompletionSource<bool> _inner;

        public TaskCompletionSource() => _inner = new TaskCompletionSource<bool>();

        public TaskCompletionSource(TaskCreationOptions options) => _inner = new TaskCompletionSource<bool>(options);

        public Task Task => _inner.Task;

        public void SetResult() => _inner.SetResult(true);

        public bool TrySetResult() => _inner.TrySetResult(true);

        public void SetCanceled() => _inner.SetCanceled();

        public bool TrySetCanceled() => _inner.TrySetCanceled();

        public void SetException(System.Exception exception) => _inner.SetException(exception);

        public bool TrySetException(System.Exception exception) => _inner.TrySetException(exception);
    }
}
