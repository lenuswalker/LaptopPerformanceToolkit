using LenovoLegionToolkit.Lib.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.AutoListeners;
using NeoSmart.AsyncLock;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Automation.Utils;

public class ProcessorManager
{
    private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();
    private readonly TimeAutoListener _timeAutoListener = IoCContainer.Resolve<TimeAutoListener>();

    private readonly AsyncLock _ioLock = new();

    // CPU limits
    private Dictionary<PowerType, int> currentLimits = new();
    private Dictionary<PowerType, int> currentMSRLimits = new();
    private Dictionary<PowerType, int> savedLimits = new();

    private double _stapm;
    private double _fast;
    private double _slow;
    private bool _useMSR;
    private int _interval;

    public ProcessorManager(TimeAutoListener timeAutoListener)
    {
        _timeAutoListener = timeAutoListener;
        // initialize processor
        _controller = _controller.GetCurrent();
    }

    public bool IsSupported()
    {
        // Need to add some logic here
        return true;
    }

    public async Task InitializeAsync()
    {
        using (await _ioLock.LockAsync().ConfigureAwait(false))
        {
            await _timeAutoListener.SubscribeChangedAsync(TimeAutoListener_Changed).ConfigureAwait(false);

            await StopAsync().ConfigureAwait(false);
        }
    }

    public Task StartAsync(double stapm, double fast, double slow, bool useMSR, int interval)
    {
        _stapm = stapm;
        _fast = fast;
        _slow = slow;
        _useMSR = useMSR;
        _interval = interval;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting processor TDP/power limits...");

        MaintainTDP(_stapm, _fast, _slow, _useMSR).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Processor TDP/power limits set");

        if (_interval > 0)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting time interval listener...");

            _timeAutoListener.StartNowAsync(_interval);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Started time interval listener.");
        }
        
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopping time interval listener...");

        _timeAutoListener.StopNowAsync();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped time interval listener.");

        _stapm = 0;
        _fast = 0;
        _slow = 0;
        _useMSR = false;

        return Task.CompletedTask;
    }

    public void SetTDPLimit(PowerType type, double limit)
    {
        _controller.SetTDPLimit(type, limit);
    }

    public void SetMSRLimits(double slow, double fast)
    {
        if (_controller.GetType() == typeof(IntelProcessorController))
            ((IntelProcessorController)_controller).SetMSRLimits(slow, fast);
    }

    public Task MaintainTDP(double stapm, double fast, double slow, bool useMSR)
    {
        savedLimits = new()
        {
            {
                PowerType.Stapm,
                (int)stapm
            },
            {
                PowerType.Fast,
                (int)fast
            },
            {
                PowerType.Slow,
                (int)slow
            }
        };

        // get current limits
        foreach (PowerType type in Enum.GetValues(typeof(PowerType)))
        {
            if (_controller.GetType() == typeof(IntelProcessorController))
            {
                // Intel doesn't have stapm
                if (type == PowerType.Stapm)
                    continue;
            }

            int limit = _controller.GetTDPLimit(type);

            if (limit == 0)
                continue;
            else
            {
                if (currentLimits.ContainsKey(type))
                    currentLimits[type] = limit;
                else
                    currentLimits.Add(type, limit);
            }
        }

        // search for limit changes
        if (currentLimits.Any())
            foreach (KeyValuePair<PowerType, int> pair in currentLimits)
            {
                if (!savedLimits.ContainsKey(pair.Key))
                    continue;

                if (pair.Key == PowerType.Stapm)
                    continue;

                if (savedLimits[pair.Key] == pair.Value)
                    continue;

                _controller.SetTDPLimit(pair.Key, savedLimits[pair.Key]);
            }

        // processor specific
        if (useMSR)
        {
            if (_controller.GetType() == typeof(IntelProcessorController))
            {
                currentMSRLimits = ((IntelProcessorController)_controller).GetMSRLimits();

                foreach (KeyValuePair<PowerType, int> pair in currentMSRLimits)
                {
                    if (!savedLimits.ContainsKey(pair.Key))
                        continue;

                    if (savedLimits[pair.Key] == pair.Value)
                        continue;

                    // Set MSR limit
                    ((IntelProcessorController)_controller).SetMSRLimits(savedLimits[PowerType.Slow], savedLimits[PowerType.Fast]);
                }
            }
        }
        return Task.CompletedTask;
    }

    private async void TimeAutoListener_Changed(object? sender, TimeAutoListener.ChangedEventArgs args)
    {
        await MaintainTDP(_stapm, _fast, _slow, _useMSR).ConfigureAwait(false);
    }
}
