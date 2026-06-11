using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace LenovoLegionToolkit.Lib.AutoListeners;

// Raises an event for every interval (in seconds) registered via UpdateIntervalsAsync.
// One timer per distinct interval; no timers run when no pipeline needs them.
public class TimeIntervalAutoListener : AbstractAutoListener<TimeIntervalAutoListener.ChangedEventArgs>
{
    public class ChangedEventArgs(int interval) : EventArgs
    {
        public int Interval { get; } = interval;
    }

    private readonly object _lock = new();
    private readonly Dictionary<int, Timer> _timers = [];

    private HashSet<int> _intervals = [];
    private bool _started;

    public Task UpdateIntervalsAsync(IEnumerable<int> intervalsSeconds)
    {
        lock (_lock)
        {
            _intervals = intervalsSeconds.Where(i => i > 0).ToHashSet();
            if (_started)
                SyncTimers();
        }

        return Task.CompletedTask;
    }

    protected override Task StartAsync()
    {
        lock (_lock)
        {
            _started = true;
            SyncTimers();
        }

        return Task.CompletedTask;
    }

    protected override Task StopAsync()
    {
        lock (_lock)
        {
            _started = false;
            SyncTimers();
        }

        return Task.CompletedTask;
    }

    private void SyncTimers()
    {
        var wanted = _started ? _intervals : [];

        foreach (var interval in _timers.Keys.Where(i => !wanted.Contains(i)).ToArray())
        {
            _timers[interval].Dispose();
            _timers.Remove(interval);
        }

        foreach (var interval in wanted.Where(i => !_timers.ContainsKey(i)))
        {
            var timer = new Timer(interval * 1000) { AutoReset = true };
            timer.Elapsed += (_, _) => RaiseChanged(new ChangedEventArgs(interval));
            timer.Start();
            _timers[interval] = timer;
        }
    }
}
