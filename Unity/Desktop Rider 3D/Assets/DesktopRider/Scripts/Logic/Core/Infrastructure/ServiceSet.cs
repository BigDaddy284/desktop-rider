#nullable enable

using System;
using System.Collections.Generic;

namespace DesktopRider.Core.Infrastructure
{
    /// <summary>
    /// A group of parts that are each made once, the first time they are asked for, and closed down together
    /// in the opposite order. It does the small job version 1 gave to a dependency-injection package.
    /// </summary>
    public abstract class ServiceSet : IDisposable
    {
        private readonly object _gate = new();
        private readonly Stack<IDisposable> _made = new();
        private bool _disposed;

        /// <summary>Returns the part held in <paramref name="held"/>, making it first if this is the first request.</summary>
        protected T Once<T>(ref T? held, Func<T> make)
            where T : class
        {
            Guard.NotNull(make, nameof(make));
            lock (_gate)
            {
                Guard.NotDisposed(_disposed, this);
                if (held is null)
                {
                    held = make();
                    if (held is IDisposable disposable)
                    {
                        _made.Push(disposable);
                    }
                }

                return held;
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                while (_made.Count > 0)
                {
                    _made.Pop().Dispose();
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
