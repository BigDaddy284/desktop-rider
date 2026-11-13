#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// In the namespace that encloses every test, so the tests can use the short name.
namespace DesktopRider
{
    /// <summary>
    /// A stream a test feeds a little at a time, to stand in for a reply arriving slowly over the network.
    /// (Version 1's tests used System.IO.Pipelines, which Unity does not include.)
    /// </summary>
    internal sealed class Pipe
    {
        private readonly object _gate = new();
        private readonly Queue<byte[]> _waiting = new();
        private TaskCompletionSource<bool> _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _completed;
        private bool _readerClosed;
        private Exception? _failure;

        public Pipe()
        {
            Writer = new PipeWriter(this);
            Reader = new PipeReader(this);
        }

        public PipeWriter Writer { get; }

        public PipeReader Reader { get; }

        private void Signal()
        {
            var changed = _changed;
            _changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            changed.TrySetResult(true);
        }

        internal sealed class PipeWriter
        {
            private readonly Pipe _pipe;

            public PipeWriter(Pipe pipe) => _pipe = pipe;

            public Task WriteAsync(byte[] bytes)
            {
                lock (_pipe._gate)
                {
                    _pipe._waiting.Enqueue(bytes);
                    _pipe.Signal();
                }

                return Task.CompletedTask;
            }

            /// <summary>Ends the stream. With an exception, the reader fails with it once everything written has been read.</summary>
            public Task CompleteAsync(Exception? failure = null)
            {
                lock (_pipe._gate)
                {
                    _pipe._completed = true;
                    _pipe._failure = failure;
                    _pipe.Signal();
                }

                return Task.CompletedTask;
            }
        }

        internal sealed class PipeReader
        {
            private readonly Pipe _pipe;

            public PipeReader(Pipe pipe) => _pipe = pipe;

            public Stream AsStream() => new ReadingStream(_pipe);
        }

        private sealed class ReadingStream : Stream
        {
            private readonly Pipe _pipe;
            private byte[]? _current;
            private int _used;

            public ReadingStream(Pipe pipe) => _pipe = pipe;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                while (true)
                {
                    Task changed;
                    lock (_pipe._gate)
                    {
                        if (_pipe._readerClosed)
                        {
                            throw new IOException("The connection was closed.");
                        }

                        if (_current is null && _pipe._waiting.Count > 0)
                        {
                            _current = _pipe._waiting.Dequeue();
                            _used = 0;
                        }

                        if (_current is not null)
                        {
                            var size = Math.Min(count, _current.Length - _used);
                            Array.Copy(_current, _used, buffer, offset, size);
                            _used += size;
                            if (_used == _current.Length)
                            {
                                _current = null;
                            }

                            return size;
                        }

                        if (_pipe._completed)
                        {
                            return _pipe._failure is null ? 0 : throw _pipe._failure;
                        }

                        changed = _pipe._changed.Task;
                    }

                    var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using (cancellationToken.Register(() => stopped.TrySetResult(true)))
                    {
                        await Task.WhenAny(changed, stopped.Task).ConfigureAwait(false);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            public override int Read(byte[] buffer, int offset, int count) =>
                ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    lock (_pipe._gate)
                    {
                        _pipe._readerClosed = true;
                        _pipe.Signal();
                    }
                }

                base.Dispose(disposing);
            }
        }
    }
}
