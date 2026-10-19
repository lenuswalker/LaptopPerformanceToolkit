using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32;

namespace LenovoLegionToolkit.Lib.Services;

public class GPUKeepOffMonitorService(GPUController gpuController, ApplicationSettings settings)
{
    private static readonly TimeSpan BatteryPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AcPollInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RestartCooldown = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SleepVerificationTimeout = TimeSpan.FromMinutes(3);

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private readonly object _transitionLock = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _transitionCts;
    private Task? _monitorTask;
    private volatile bool _suspended;
    private volatile bool _onAc;
    private int _powerGeneration;
    private volatile GPUPowerSavingStatus _status = GPUPowerSavingStatus.Disabled;

    public GPUPowerSavingStatus Status { get => _status; private set => SetStatus(value); }
    public event EventHandler<GPUPowerSavingStatus>? StatusChanged;

    public async Task StartStopIfNeededAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
            if (!settings.Store.KeepGPUOffOnBattery)
            {
                Status = GPUPowerSavingStatus.Disabled;
                return;
            }

            if (!gpuController.IsSupported())
            {
                Status = GPUPowerSavingStatus.Unsupported;
                return;
            }

            _onAc = Power.IsPowerAdapterConnected();
            _suspended = false;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            SystemEvents.PowerModeChanged += PowerModeChanged;
            _monitorTask = Task.Run(() => MonitorLoopAsync(token));
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
            Status = GPUPowerSavingStatus.Disabled;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        SystemEvents.PowerModeChanged -= PowerModeChanged;
        lock (_transitionLock)
            _transitionCts?.Cancel();
        if (_cts is not null)
            await _cts.CancelAsync().ConfigureAwait(false);
        if (_monitorTask is not null)
        {
            try { await _monitorTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _cts?.Dispose();
        _cts = null;
        _monitorTask = null;
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        IDisposable? nvapiSession = null;
        try
        {
            try
            {
                nvapiSession = NVAPI.AcquireSession();
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to initialize NVAPI session.", ex);
            }

            var inactiveReadings = 0;
            var lastRestart = DateTime.MinValue;
            var retryAt = DateTime.MinValue;
            var waitingForSleepUntil = DateTime.MinValue;
            var lastInactiveReadAt = -1L;
            var observedGeneration = Volatile.Read(ref _powerGeneration);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var currentGeneration = Volatile.Read(ref _powerGeneration);
                    if (currentGeneration != observedGeneration)
                    {
                        observedGeneration = currentGeneration;
                        inactiveReadings = 0;
                        lastInactiveReadAt = -1L;
                        waitingForSleepUntil = DateTime.MinValue;
                        retryAt = DateTime.MinValue;
                    }

                    if (_suspended)
                    {
                        inactiveReadings = 0;
                        lastInactiveReadAt = -1L;
                        waitingForSleepUntil = DateTime.MinValue;
                        Status = GPUPowerSavingStatus.Suspended;
                        await WaitForWakeAsync(AcPollInterval, token).ConfigureAwait(false);
                        continue;
                    }

                    var isOnAc = Power.IsPowerAdapterConnected();
                    if (isOnAc != _onAc)
                    {
                        _onAc = isOnAc;
                        Interlocked.Increment(ref _powerGeneration);
                        observedGeneration = Volatile.Read(ref _powerGeneration);
                        inactiveReadings = 0;
                        lastInactiveReadAt = -1L;
                        waitingForSleepUntil = DateTime.MinValue;
                        retryAt = DateTime.MinValue;
                    }

                    if (isOnAc)
                    {
                        inactiveReadings = 0;
                        lastInactiveReadAt = -1L;
                        waitingForSleepUntil = DateTime.MinValue;
                        Status = GPUPowerSavingStatus.OnAC;
                        await WaitForWakeAsync(AcPollInterval, token).ConfigureAwait(false);
                        continue;
                    }

                    Status = waitingForSleepUntil > DateTime.UtcNow ? GPUPowerSavingStatus.WaitingForSleep : GPUPowerSavingStatus.Checking;
                    var generation = Volatile.Read(ref _powerGeneration);
                    var gpuStatus = await gpuController.RefreshNowAsync(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();

                    if (_suspended || Power.IsPowerAdapterConnected() || generation != Volatile.Read(ref _powerGeneration))
                    {
                        inactiveReadings = 0;
                        lastInactiveReadAt = -1L;
                        waitingForSleepUntil = DateTime.MinValue;
                        retryAt = DateTime.MinValue;
                        continue;
                    }

                    if (gpuStatus.State is GPUState.PoweredOff)
                    {
                        inactiveReadings = 0;
                        lastInactiveReadAt = -1L;
                        waitingForSleepUntil = DateTime.MinValue;
                        Status = GPUPowerSavingStatus.Asleep;
                    }
                    else if (gpuStatus.State is GPUState.Active)
                    {
                        inactiveReadings = 0;
                        lastInactiveReadAt = -1L;
                        waitingForSleepUntil = DateTime.MinValue;
                        Status = GPUPowerSavingStatus.ActiveApplications;
                    }
                    else if (gpuStatus.State is GPUState.MonitorConnected)
                    {
                        inactiveReadings = 0;
                        lastInactiveReadAt = -1L;
                        waitingForSleepUntil = DateTime.MinValue;
                        Status = GPUPowerSavingStatus.DisplayConnected;
                    }
                    else if (gpuStatus.State is GPUState.Inactive)
                    {
                        if (waitingForSleepUntil > DateTime.UtcNow)
                        {
                            Status = GPUPowerSavingStatus.WaitingForSleep;
                        }
                        else if (DateTime.UtcNow < retryAt)
                        {
                            Status = GPUPowerSavingStatus.RestartFailed;
                        }
                        else if (DateTime.UtcNow - lastRestart < RestartCooldown)
                        {
                            Status = GPUPowerSavingStatus.WaitingForIdle;
                        }
                        else if (inactiveReadings == 0)
                        {
                            inactiveReadings = 1;
                            lastInactiveReadAt = Environment.TickCount64;
                            Status = GPUPowerSavingStatus.WaitingForIdle;
                        }
                        else if (Environment.TickCount64 - lastInactiveReadAt < (long)BatteryPollInterval.TotalMilliseconds)
                        {
                            Status = GPUPowerSavingStatus.WaitingForIdle;
                        }
                        else
                        {
                            var restartGeneration = Volatile.Read(ref _powerGeneration);
                            await RestartIfStillIdleAsync(token).ConfigureAwait(false);
                            if (restartGeneration != Volatile.Read(ref _powerGeneration))
                            {
                                inactiveReadings = 0;
                                waitingForSleepUntil = DateTime.MinValue;
                                retryAt = DateTime.MinValue;
                                continue;
                            }
                            if (Status is GPUPowerSavingStatus.WaitingForSleep)
                            {
                                lastRestart = DateTime.UtcNow;
                                waitingForSleepUntil = lastRestart + SleepVerificationTimeout;
                            }
                            else if (Status is GPUPowerSavingStatus.RestartFailed)
                            {
                                retryAt = DateTime.UtcNow + FailureRetryDelay;
                            }
                            inactiveReadings = 0;
                            lastInactiveReadAt = -1L;
                        }
                    }
                    else
                    {
                        inactiveReadings = 0;
                        lastInactiveReadAt = -1L;
                        Status = waitingForSleepUntil > DateTime.UtcNow ? GPUPowerSavingStatus.WaitingForSleep : GPUPowerSavingStatus.Unknown;
                    }

                    if (waitingForSleepUntil != DateTime.MinValue && DateTime.UtcNow >= waitingForSleepUntil)
                    {
                        waitingForSleepUntil = DateTime.MinValue;
                        retryAt = DateTime.UtcNow + FailureRetryDelay;
                        Status = GPUPowerSavingStatus.RestartFailed;
                    }

                    await WaitForWakeAsync(BatteryPollInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    inactiveReadings = 0;
                    lastInactiveReadAt = -1L;
                    Status = GPUPowerSavingStatus.Unknown;
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"GPU keep-off monitor iteration failed.", ex);
                    await WaitForWakeAsync(BatteryPollInterval, token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            try { nvapiSession?.Dispose(); }
            catch { }
        }
    }

    private async Task RestartIfStillIdleAsync(CancellationToken token)
    {
        var generation = Volatile.Read(ref _powerGeneration);
        if (_suspended || Power.IsPowerAdapterConnected())
        {
            Status = _suspended ? GPUPowerSavingStatus.Suspended : GPUPowerSavingStatus.OnAC;
            return;
        }

        var freshStatus = await gpuController.RefreshNowAsync(token).ConfigureAwait(false);
        if (generation != Volatile.Read(ref _powerGeneration))
            return;
        if (freshStatus.State is not GPUState.Inactive || _suspended || Power.IsPowerAdapterConnected())
        {
            Status = freshStatus.State switch
            {
                GPUState.Active => GPUPowerSavingStatus.ActiveApplications,
                GPUState.MonitorConnected => GPUPowerSavingStatus.DisplayConnected,
                _ when _suspended => GPUPowerSavingStatus.Suspended,
                _ when Power.IsPowerAdapterConnected() => GPUPowerSavingStatus.OnAC,
                _ => GPUPowerSavingStatus.WaitingForIdle,
            };
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        lock (_transitionLock)
        {
            _transitionCts = linkedCts;
            if (_suspended || Power.IsPowerAdapterConnected())
                linkedCts.Cancel();
        }
        try
        {
            if (linkedCts.IsCancellationRequested)
            {
                Status = _suspended ? GPUPowerSavingStatus.Suspended : GPUPowerSavingStatus.OnAC;
                return;
            }
            Status = GPUPowerSavingStatus.Restarting;
            var result = await gpuController.RestartGPUAsync(onlyIfIdle: true, linkedCts.Token).ConfigureAwait(false);
            if (generation != Volatile.Read(ref _powerGeneration))
                return;
            if (_suspended || Power.IsPowerAdapterConnected())
            {
                Status = _suspended ? GPUPowerSavingStatus.Suspended : GPUPowerSavingStatus.OnAC;
                return;
            }
            Status = result switch
            {
                GPURestartResult.Restarted => GPUPowerSavingStatus.WaitingForSleep,
                GPURestartResult.Skipped => _suspended ? GPUPowerSavingStatus.Suspended : GPUPowerSavingStatus.WaitingForIdle,
                GPURestartResult.TimedOut or GPURestartResult.Failed => GPUPowerSavingStatus.RestartFailed,
                _ => GPUPowerSavingStatus.RestartFailed,
            };
        }
        catch (OperationCanceledException)
        {
            Status = _suspended ? GPUPowerSavingStatus.Suspended : Power.IsPowerAdapterConnected() ? GPUPowerSavingStatus.OnAC : GPUPowerSavingStatus.WaitingForIdle;
        }
        catch (Exception ex)
        {
            Status = GPUPowerSavingStatus.RestartFailed;
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GPU restart failed.", ex);
        }
        finally
        {
            lock (_transitionLock)
            {
                if (ReferenceEquals(_transitionCts, linkedCts))
                    _transitionCts = null;
            }
        }
    }

    private async Task WaitForWakeAsync(TimeSpan timeout, CancellationToken token)
    {
        await _wakeSignal.WaitAsync(timeout, token).ConfigureAwait(false);
    }

    private void PowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        try
        {
            var powerTransition = false;
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    _suspended = true;
                    powerTransition = true;
                    break;
                case PowerModes.Resume:
                    _suspended = false;
                    _onAc = Power.IsPowerAdapterConnected();
                    powerTransition = true;
                    break;
                case PowerModes.StatusChange:
                    var onAc = Power.IsPowerAdapterConnected();
                    powerTransition = onAc != _onAc;
                    _onAc = onAc;
                    break;
            }

            if (powerTransition)
            {
                Interlocked.Increment(ref _powerGeneration);
                try
                {
                    lock (_transitionLock)
                        _transitionCts?.Cancel();
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to cancel pending GPU restart.", ex);
                }

                if (_suspended)
                    Status = GPUPowerSavingStatus.Suspended;
                else if (_onAc)
                    Status = GPUPowerSavingStatus.OnAC;
                else
                    Status = GPUPowerSavingStatus.WaitingForIdle;
            }

            try
            {
                if (_wakeSignal.CurrentCount == 0)
                    _wakeSignal.Release();
            }
            catch (ObjectDisposedException) { }
            catch (SemaphoreFullException) { }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed handling power mode event.", ex);
        }
    }

    private void SetStatus(GPUPowerSavingStatus status)
    {
        if (_suspended && status is not GPUPowerSavingStatus.Suspended and not GPUPowerSavingStatus.Disabled and not GPUPowerSavingStatus.Unsupported)
            return;
        if (_onAc && status is not GPUPowerSavingStatus.OnAC and not GPUPowerSavingStatus.Disabled and not GPUPowerSavingStatus.Unsupported)
            return;
        if (_status == status)
            return;
        _status = status;
        try { StatusChanged?.Invoke(this, status); }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GPU power saving status handler failed.", ex);
        }
    }
}
