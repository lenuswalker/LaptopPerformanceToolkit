using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Utils;
using NeoSmart.AsyncLock;
using Timer = System.Timers.Timer;

namespace LenovoLegionToolkit.Lib.Automation.Utils;

// Applies processor TDP limits and optionally re-applies them on an interval,
// since firmware or vendor software can silently reset them.
public class ProcessorManager(ProcessorController controller)
{
    private readonly AsyncLock _stateLock = new();

    private Timer? _timer;
    private ProcessorTDPState _state;
    private int _applying;

    public Task<bool> IsSupportedAsync() => controller.IsSupportedAsync();

    public async Task ApplyAsync(ProcessorTDPState state)
    {
        using (await _stateLock.LockAsync().ConfigureAwait(false))
        {
            _state = state;
            StopTimer();
        }

        await ApplyLimitsAsync(true).ConfigureAwait(false);

        if (state.MaintainTDP && state.Interval > 0)
        {
            using (await _stateLock.LockAsync().ConfigureAwait(false))
                StartTimer(state.Interval);
        }
    }

    public async Task StopAsync()
    {
        using (await _stateLock.LockAsync().ConfigureAwait(false))
            StopTimer();
    }

    private void StartTimer(int intervalSeconds)
    {
        _timer = new Timer(intervalSeconds * 1000) { AutoReset = true };
        _timer.Elapsed += Timer_Elapsed;
        _timer.Start();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started TDP maintain timer. [interval={intervalSeconds}s]");
    }

    private void StopTimer()
    {
        if (_timer is null)
            return;

        _timer.Elapsed -= Timer_Elapsed;
        _timer.Dispose();
        _timer = null;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped TDP maintain timer.");
    }

    private async void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _applying, 1, 0) != 0)
            return;

        try
        {
            await ApplyLimitsAsync(false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to maintain TDP limits.", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _applying, 0);
        }
    }

    private async Task ApplyLimitsAsync(bool force)
    {
        if (!await controller.IsSupportedAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Processor TDP control is not supported.");

            return;
        }

        ProcessorTDPState state;
        using (await _stateLock.LockAsync().ConfigureAwait(false))
            state = _state;

        if (state.UseMSR && controller.SupportsMSR)
        {
            if (state.Fast > 0 && state.Slow > 0)
                await controller.SetMSRLimitsAsync((int)state.Slow, (int)state.Fast).ConfigureAwait(false);

            return;
        }

        var targets = new Dictionary<PowerType, int>();
        if (controller.SupportsStapm && state.Stapm > 0)
            targets[PowerType.Stapm] = (int)state.Stapm;
        if (state.Fast > 0)
            targets[PowerType.Fast] = (int)state.Fast;
        if (state.Slow > 0)
            targets[PowerType.Slow] = (int)state.Slow;

        if (targets.Count == 0)
            return;

        var current = force
            ? new Dictionary<PowerType, int>()
            : await controller.GetTDPLimitsAsync().ConfigureAwait(false);

        foreach (var (type, watts) in targets)
        {
            if (!force && current.TryGetValue(type, out var currentWatts) && currentWatts == watts)
                continue;

            await controller.SetTDPLimitAsync(type, watts).ConfigureAwait(false);
        }
    }
}
