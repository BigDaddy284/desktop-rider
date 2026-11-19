#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopRider.Animation
{
    /// <summary>
    /// Decides which frame of which animation is showing. It has no timer and draws nothing: the caller tells it how much
    /// time has passed and reads back the current frame, so it runs the same whether or not the AI is busy.
    /// </summary>
    public sealed class AnimationPlayer
    {
        private readonly SpriteManifest _manifest;
        private readonly Queue<AnimationClip> _queue = new();
        private AnimationClip _baseLoop;
        private (AnimationClip Clip, int Priority)? _pending;
        private int _priority;
        private double _secondsInClip;
        private double _speed = 1.0;

        public AnimationPlayer(SpriteManifest manifest)
        {
            _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            _baseLoop = manifest.Default;
            Current = manifest.Default;
        }

        /// <summary>Raised whenever the frame to draw changes.</summary>
        public event EventHandler? FrameChanged;

        public AnimationClip Current { get; private set; }

        public int FrameIndex { get; private set; }

        /// <summary>True while the character is in its resting loop with nothing else waiting.</summary>
        public bool IsResting => Current == _manifest.Default && _queue.Count == 0 && _pending is null;

        /// <summary>Playback speed, where 1.0 is normal.</summary>
        public double Speed
        {
            get => _speed;
            set => _speed = double.IsFinite(value) && value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Speed must be above zero.");
        }

        /// <summary>
        /// Asks for an animation. A loop that is playing gives way at once (through its finishing animation if it has one).
        /// A one-shot that is playing is allowed to finish first, and refuses requests of lower priority.
        /// </summary>
        /// <returns>False if a more important animation is playing and the request was dropped.</returns>
        public bool Request(string name, int priority = 0)
        {
            var target = _manifest[name];
            if (!Current.Loop)
            {
                if (priority < _priority || (_pending is { } waiting && priority < waiting.Priority))
                {
                    return false;
                }

                _pending = (target, priority);
                return true;
            }

            Start(target, priority, Current);
            return true;
        }

        /// <summary>Moves time forward.</summary>
        /// <returns>True if the frame to draw changed.</returns>
        public bool Advance(TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
            {
                return false;
            }

            var before = (Current, FrameIndex);
            // Ticks divided by ticks-per-second, not TotalSeconds: Unity's Mono runtime computes TotalSeconds as ticks * 1e-7,
            // which lands a hair under exact frame boundaries (0.2 s becomes 0.19999...) and so shows the previous frame.
            _secondsInClip += elapsed.Ticks / (double)TimeSpan.TicksPerSecond * _speed;
            var fps = _manifest.FramesPerSecond;

            // Each pass either settles on a frame or moves to the next animation; the cap only guards against a broken manifest.
            for (var guard = 0; guard < 1000; guard++)
            {
                var length = Current.FrameCount / fps;
                if (Current.Loop)
                {
                    _secondsInClip %= length;
                    break;
                }

                if (_secondsInClip < length)
                {
                    break;
                }

                _secondsInClip -= length;
                Next();
            }

            FrameIndex = Math.Min((int)(_secondsInClip * fps), Current.FrameCount - 1);
            if (before == (Current, FrameIndex))
            {
                return false;
            }

            FrameChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private void Next()
        {
            if (_pending is { } waiting)
            {
                // If the animation that just ended was the lead-in to a loop, the rider is now in that loop's pose
                // and has to come back out of it before doing anything else.
                var finished = Current;
                var heading = _queue.Count > 0 ? _queue.Last() : null;
                var leaving = heading is not null && string.Equals(heading.Enter, finished.Name, StringComparison.OrdinalIgnoreCase) ? heading : null;
                _pending = null;
                var leftover = _secondsInClip;
                Start(waiting.Clip, waiting.Priority, leaving);
                _secondsInClip = leftover;
                return;
            }

            Current = _queue.TryDequeue(out var next) ? next : _baseLoop;
            if (Current.Loop)
            {
                _priority = 0;
            }
        }

        private void Start(AnimationClip target, int priority, AnimationClip? leaving)
        {
            if (target == leaving && _queue.Count == 0)
            {
                return;
            }

            _queue.Clear();
            if (leaving?.Exit is { } exit)
            {
                _queue.Enqueue(_manifest[exit]);
                _baseLoop = _manifest.Default;
            }

            if (target.Enter is { } enter)
            {
                _queue.Enqueue(_manifest[enter]);
            }

            _queue.Enqueue(target);
            if (target.Loop)
            {
                _baseLoop = target;
            }

            Current = _queue.Dequeue();
            _priority = Current.Loop ? 0 : priority;
            _secondsInClip = 0;
            if (FrameIndex != 0 || leaving != Current)
            {
                FrameIndex = 0;
                FrameChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
