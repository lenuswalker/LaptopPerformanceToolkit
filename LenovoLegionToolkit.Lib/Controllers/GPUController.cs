using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Resources;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using NeoSmart.AsyncLock;
using NvAPIWrapper.Native;
using NvAPIWrapper.Native.Exceptions;
using NvAPIWrapper.Native.General;

namespace LenovoLegionToolkit.Lib.Controllers;

public class GPUController
{
    private const Status GpuNotPoweredStatus = (Status)(-220);

    private readonly AsyncLock _lock = new();
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private Func<IDisposable> _sessionFactory = NVAPI.AcquireSession;
    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "dwm", "explorer", "csrss", "winlogon", "services", "lsass", "smss",
        "wininit", "svchost", "fontdrvhost", "nvcontainer", "nvdisplay.container", "nvwmi64",
        "taskhostw", "sihost", "runtimebroker", "shellexperiencehost", "searchhost",
        "startmenuexperiencehost", "textinputhost", "audiodg", "ctfloader", "spoolsv"
    };

    private Task? _refreshTask;
    private CancellationTokenSource? _refreshCancellationTokenSource;

    private GPUState _state = GPUState.Unknown;
    private List<Process> _processes = [];
    private string? _gpuInstanceId;
    private string? _performanceState;

    public event EventHandler<GPUStatus>? Refreshed;
    public bool IsStarted { get => _refreshTask is { IsCompleted: false }; }

    public bool IsSupported()
    {
        try
        {
            using var session = NVAPI.AcquireSession();
            return NVAPI.GetGPU() is not null;
        }
        catch
        {
            return false;
        }
    }

    public async Task<GPUState> GetLastKnownStateAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
            return _state;
    }

    public async Task<GPUStatus> RefreshNowAsync(CancellationToken token = default)
    {
        GPUStatus status;
        using (await _lock.LockAsync(token).ConfigureAwait(false))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var session = NVAPI.AcquireSession();
                await RefreshStateAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _state = GPUState.Unknown;
                _performanceState = null;
                _gpuInstanceId = null;
                _processes = [];
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"GPU status unavailable.", ex);
            }
            status = new GPUStatus(_state, _performanceState, _processes);
        }
        Refreshed?.Invoke(this, status);
        return status;
    }

    public async Task StartAsync(int delay = 1_000, int interval = 5_000)
    {
        await _lifecycleLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_refreshTask is { IsCompleted: false })
                return;

            _refreshCancellationTokenSource?.Dispose();
            _refreshCancellationTokenSource = new CancellationTokenSource();
            var token = _refreshCancellationTokenSource.Token;
            _refreshTask = Task.Run(() => RefreshLoopAsync(delay, interval, token), token);
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
            if (_refreshCancellationTokenSource is not null)
                await _refreshCancellationTokenSource.CancelAsync().ConfigureAwait(false);
            if (_refreshTask is not null)
            {
                try
                {
                    await _refreshTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }
        }
        finally
        {
            _refreshCancellationTokenSource?.Dispose();
            _refreshCancellationTokenSource = null;
            _refreshTask = null;
            _lifecycleLock.Release();
        }
    }

    public async Task<GPURestartResult> RestartGPUAsync(bool onlyIfIdle = false, CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using (await _lock.LockAsync(timeout.Token).ConfigureAwait(false))
            {
                if (onlyIfIdle && Power.IsPowerAdapterConnected())
                    return GPURestartResult.Skipped;

                using (NVAPI.AcquireSession())
                    await RefreshStateAsync(timeout.Token).ConfigureAwait(false);

                if (_state is not GPUState.Active and not GPUState.Inactive || string.IsNullOrEmpty(_gpuInstanceId))
                    return GPURestartResult.Skipped;
                if (onlyIfIdle && (_state != GPUState.Inactive || Power.IsPowerAdapterConnected()))
                    return GPURestartResult.Skipped;

                timeout.Token.ThrowIfCancellationRequested();
                var (exitCode, output) = await CMD.RunAsync("pnputil", $"/restart-device \"{_gpuInstanceId}\"",
                    token: timeout.Token, killOnCancellation: true).ConfigureAwait(false);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"GPU restart finished. [exitCode={exitCode}, output={output}]");

                _state = GPUState.Unknown;
                _gpuInstanceId = null;
                _performanceState = null;
                return exitCode == 0 ? GPURestartResult.Restarted : GPURestartResult.Failed;
            }
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return GPURestartResult.TimedOut;
        }
    }

    public async Task KillGPUProcessesAsync(CancellationToken token = default)
    {
        using (await _lock.LockAsync(token).ConfigureAwait(false))
        {
            using var session = NVAPI.AcquireSession();
            await RefreshStateAsync(token).ConfigureAwait(false);
            if (_state is not GPUState.Active)
                return;

            using var currentProcess = Process.GetCurrentProcess();
            var failures = new List<Exception>();
            foreach (var process in _processes)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (process.HasExited || process.Id == Environment.ProcessId || process.SessionId != currentProcess.SessionId ||
                        ProtectedProcesses.Contains(process.ProcessName))
                        continue;

                    process.Kill();
                    await process.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    failures.Add(ex);
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Couldn't stop GPU process. [pid={process.Id}]", ex);
                }
            }
            if (failures.Count != 0)
                throw new AggregateException(failures);
        }
    }

    private async Task RefreshLoopAsync(int delay, int interval, CancellationToken token)
    {
        IDisposable? session = null;
        try
        {
            await Task.Delay(delay, token).ConfigureAwait(false);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    session ??= _sessionFactory();
                    await RefreshNowAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    session?.Dispose();
                    session = null;
                    try
                    {
                        await PublishUnknownAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"GPU refresh failed; will retry.", ex);
                }
                if (interval <= 0)
                    break;
                await Task.Delay(interval, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            session?.Dispose();
        }
    }

    private async Task PublishUnknownAsync(CancellationToken token)
    {
        GPUStatus status;
        using (await _lock.LockAsync(token).ConfigureAwait(false))
        {
            _state = GPUState.Unknown;
            _performanceState = null;
            _gpuInstanceId = null;
            _processes = [];
            status = new GPUStatus(_state, _performanceState, _processes);
        }
        Refreshed?.Invoke(this, status);
    }

    private async Task RefreshStateAsync(CancellationToken token)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Refresh in progress...");

        _state = GPUState.Unknown;
        _processes = [];
        _gpuInstanceId = null;
        _performanceState = null;

        var gpu = NVAPI.GetGPU();
        if (gpu is null)
        {
            _state = GPUState.NvidiaGpuNotFound;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GPU present [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}]");

            return;
        }

        try
        {
            var stateId = GPUApi.GetCurrentPerformanceState(gpu.Handle).ToString().Split('_')[0];
            _performanceState = Resource.GPUController_PoweredOn;
            if (!string.IsNullOrWhiteSpace(stateId))
                _performanceState += $", {stateId}";
        }
        catch (NVIDIAApiException ex) when (IsGpuNotPoweredException(ex))
        {
            _state = GPUState.PoweredOff;
            _performanceState = Resource.GPUController_PoweredOff;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Powered off [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}]");

            return;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GPU status exception.", ex);

            _performanceState = "Unknown";
            return;
        }

        var pnpDeviceIdPart = NVAPI.GetGPUId(gpu);

        if (string.IsNullOrEmpty(pnpDeviceIdPart))
            throw new InvalidOperationException("pnpDeviceIdPart is null or empty");

        var gpuInstanceId = await WMI.Win32.PnpEntity.GetDeviceIDAsync(pnpDeviceIdPart).WaitAsync(token).ConfigureAwait(false);
        var processNames = NVAPIExtensions.GetActiveProcesses(gpu);

        if (NVAPI.IsDisplayConnected(gpu))
        {
            _processes = processNames;
            _state = GPUState.MonitorConnected;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace(
                    $"Monitor connected [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}]");
        }
        else if (processNames.Count != 0)
        {
            _processes = processNames;
            _state = GPUState.Active;
            _gpuInstanceId = gpuInstanceId;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Active [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}, pnpDeviceIdPart={pnpDeviceIdPart}]");
        }
        else
        {
            _state = GPUState.Inactive;
            _gpuInstanceId = gpuInstanceId;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Inactive [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}]");
        }
    }

    private static bool IsGpuNotPoweredException(Exception exception) =>
        exception is NVIDIAApiException { Status: GpuNotPoweredStatus };
}
