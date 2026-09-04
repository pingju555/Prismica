using System;
using System.Collections.Generic;
using System.Timers;

namespace Prismica.Core.Scheduling;

using Timer = System.Timers.Timer;

public sealed class DefaultFrameScheduler : IFrameScheduler
{
    private readonly Timer _timer;
    private readonly List<FrameCallback> _callbacks = new();
    private readonly Dictionary<Guid, AnimationEntry> _animations = new();
    private long _frameId;
    private DateTime _startTime;
    private DateTime _lastFrameTime;
    private int _targetFps = 60;
    private bool _running;

    public DefaultFrameScheduler()
    {
        _timer = new Timer(1000.0 / 60.0) { AutoReset = true };
        _timer.Elapsed += OnTick;
    }

    public void Start()
    {
        if (_running) return;
        _startTime = DateTime.UtcNow;
        _lastFrameTime = _startTime;
        _running = true;
        _timer.Start();
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _timer.Stop();
    }

    public IDisposable RegisterFrameCallback(Action<FrameContext> callback, FramePriority priority = FramePriority.Normal)
    {
        var entry = new FrameCallback(callback, priority);
        lock (_callbacks) { _callbacks.Add(entry); }
        return new CallbackDisposable(() => RemoveCallback(entry));
    }

    private void RemoveCallback(FrameCallback entry)
    {
        lock (_callbacks) { _callbacks.Remove(entry); }
    }

    public AnimationHandle RegisterAnimation(AnimationDefinition def)
    {
        var handle = new AnimationHandle(Guid.NewGuid());
        var entry = new AnimationEntry(def, DateTime.UtcNow);
        lock (_animations) { _animations[handle.Id] = entry; }
        return handle;
    }

    public void CancelAnimation(AnimationHandle handle)
    {
        lock (_animations) { _animations.Remove(handle.Id); }
    }

    public void SetTargetFps(int fps)
    {
        _targetFps = Math.Clamp(fps, 1, 144);
        _timer.Interval = 1000.0 / _targetFps;
    }

    public FrameContext CurrentFrame { get; private set; }

    /// <inheritdoc />
    public int ActiveAnimationCount
    {
        get { lock (_animations) return _animations.Count; }
    }

    private void OnTick(object? sender, ElapsedEventArgs e)
    {
        if (!_running) return;

        var now = DateTime.UtcNow;
        var delta = now - _lastFrameTime;
        _lastFrameTime = now;
        var elapsed = now - _startTime;

        _frameId++;
        var interp = Math.Clamp(delta.TotalMilliseconds / (1000.0 / _targetFps), 0, 2.0);

        var ctx = new FrameContext(_frameId, elapsed, delta, interp, false);
        CurrentFrame = ctx;

        List<FrameCallback> callbacksCopy;
        lock (_callbacks) callbacksCopy = _callbacks.OrderBy(c => c.Priority).ToList();

        foreach (var cb in callbacksCopy)
        {
            try { cb.Callback(ctx); } catch { /* swallow */ }
        }

        var completed = new List<Guid>();
        lock (_animations)
        {
            foreach (var (id, entry) in _animations)
            {
                var animElapsed = now - entry.StartTime;
                var progress = Math.Min(1.0, animElapsed.TotalMilliseconds / entry.Definition.Duration.TotalMilliseconds);
                var eased = entry.Definition.Easing(progress);
                try { entry.Definition.OnProgress(eased); } catch { }

                if (progress >= 1.0)
                {
                    if (entry.Definition.AutoReverse)
                    {
                        entry.Reversed = !entry.Reversed;
                        entry.StartTime = now;
                        if (!entry.Reversed)
                        {
                            entry.RepeatCount--;
                            if (entry.Definition.RepeatCount > 0 && entry.RepeatCount <= 0)
                                completed.Add(id);
                        }
                    }
                    else
                    {
                        entry.RepeatCount--;
                        if (entry.Definition.RepeatCount <= 0 || entry.RepeatCount <= 0)
                        {
                            completed.Add(id);
                            try { entry.Definition.OnCompleted?.Invoke(); } catch { }
                        }
                        else
                        {
                            entry.StartTime = now;
                        }
                    }
                }
            }
            foreach (var id in completed) _animations.Remove(id);
        }
    }

    public void Dispose() => Stop();

    private sealed class FrameCallback(Action<FrameContext> callback, FramePriority priority)
    {
        public Action<FrameContext> Callback { get; } = callback;
        public FramePriority Priority { get; } = priority;
    }

    private sealed class CallbackDisposable(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    private sealed class AnimationEntry
    {
        public AnimationDefinition Definition { get; }
        public DateTime StartTime { get; set; }
        public bool Reversed { get; set; }
        public int RepeatCount { get; set; }

        public AnimationEntry(AnimationDefinition def, DateTime startTime)
        {
            Definition = def;
            StartTime = startTime;
            RepeatCount = def.RepeatCount;
        }
    }
}