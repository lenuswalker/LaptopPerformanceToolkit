using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Services;

public class GPUKeepOffMonitorService(GPUController gpuController, ApplicationSettings settings)
{
    private static readonly TimeSpan BatteryPollInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AcPollInterval = TimeSpan.FromSeconds(20);
    // After a device restart, Windows needs time to idle the GPU back into D3.
    // Restarting again too early re-enumerates the device and resets that idle timer.
    private static readonly TimeSpan RestartCooldown = TimeSpan.FromMinutes(3);
    private const int REQUIRED_CONSECUTIVE_INACTIVE_CHECKS = 2;

    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    public async Task StartStopIfNeededAsync()
    {
        await StopAsync().ConfigureAwait(false);

        if (!settings.Store.KeepGPUOffOnBattery)
            return;

        if (!gpuController.IsSupported())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Discrete GPU not supported, not starting.");

            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _monitorTask = Task.Run(() => MonitorLoopAsync(token), token);
    }

    public async Task StopAsync()
    {
        if (_cts is not null)
            await _cts.CancelAsync().ConfigureAwait(false);

        _cts = null;

        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
        }

        _monitorTask = null;
    }

    private async Task MonitorLoopAsync(CancellationToken token)
    {
        // Hold one NVAPI session for the whole loop, like the dashboard's refresh loop does.
        // A fresh driver session init per poll touches the GPU hard enough to reset its
        // runtime idle timer, which keeps the GPU from ever powering down between polls.
        var nvapiSessionHeld = false;
        try
        {
            NVAPI.Initialize();
            nvapiSessionHeld = true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to initialize NVAPI session.", ex);
        }

        try
        {
            await MonitorLoopInternalAsync(token).ConfigureAwait(false);
        }
        finally
        {
            if (nvapiSessionHeld)
            {
                try
                {
                    NVAPI.Unload();
                }
                catch { /* Ignored. */ }
            }
        }
    }

    private async Task MonitorLoopInternalAsync(CancellationToken token)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started.");

        var consecutiveInactive = 0;
        var lastRestart = DateTime.MinValue;

        while (!token.IsCancellationRequested)
        {
            try
            {
                if (Power.IsPowerAdapterConnected())
                {
                    consecutiveInactive = 0;
                    await Task.Delay(AcPollInterval, token).ConfigureAwait(false);
                    continue;
                }

                var status = await gpuController.RefreshNowAsync().ConfigureAwait(false);

                if (status.State is GPUState.Inactive)
                {
                    consecutiveInactive++;

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Discrete GPU is powered on but idle while on battery. [consecutiveInactive={consecutiveInactive}]");

                    if (consecutiveInactive >= REQUIRED_CONSECUTIVE_INACTIVE_CHECKS && DateTime.UtcNow - lastRestart >= RestartCooldown)
                    {
                        // Re-check right before restarting in case something just started using the GPU or AC was plugged in.
                        status = await gpuController.RefreshNowAsync().ConfigureAwait(false);

                        if (status.State is GPUState.Inactive && !Power.IsPowerAdapterConnected())
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Powering off idle discrete GPU...");

                            await gpuController.RestartGPUAsync().ConfigureAwait(false);

                            lastRestart = DateTime.UtcNow;
                            consecutiveInactive = 0;
                        }
                    }
                }
                else
                {
                    consecutiveInactive = 0;
                }

                await Task.Delay(BatteryPollInterval, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Monitor loop failed.", ex);

                try
                {
                    await Task.Delay(BatteryPollInterval, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped.");
    }
}
