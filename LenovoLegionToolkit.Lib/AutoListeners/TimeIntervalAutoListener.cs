using LenovoLegionToolkit.Lib.Extensions;
using System;
using System.Threading.Tasks;
using System.Timers;

namespace LenovoLegionToolkit.Lib.AutoListeners;

public class TimeIntervalAutoListener : AbstractAutoListener<int>
{
    private readonly Timer _timer;
    
    public TimeIntervalAutoListener()
    {
        _timer = new Timer(60_000);
        _timer.Elapsed += Timer_Elapsed;
        _timer.AutoReset = true;
    }
    
    protected override Task StartAsync()
    {
        if (!_timer.Enabled)
            _timer.Enabled = true;
        else
        {
            _timer.Enabled = false;
            _timer.Enabled = true;
        }

        return Task.CompletedTask;
    }

    public Task StartAsync(int interval)
    {
        if (!_timer.Enabled)
        {
            _timer.Interval = interval;
            _timer.Enabled = true;
        }
        else
        {
            _timer.Enabled = false;
            _timer.Interval = interval;
            _timer.Enabled = true;
        }

        return Task.CompletedTask;
    }

    protected override Task StopAsync()
    {
        _timer.Enabled = false;

        return Task.CompletedTask;
    }

    public Task StopNowAsync()
    {
        _timer.Enabled = false;

        return Task.CompletedTask;
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e) => RaiseChanged((int)_timer.Interval);
}
