using System;
using System.Threading.Tasks;
using System.Timers;
using LenovoLegionToolkit.Lib.Listeners;

namespace LenovoLegionToolkit.Lib.Automation.Listeners
{
    public class TimeIntervalAutomationListener : IListener<int>
    {
        public event EventHandler<int>? Changed;

        private readonly Timer _timer;
        
        public TimeIntervalAutomationListener()
        {
            _timer = new Timer(5_000);
            _timer.Elapsed += Timer_Elapsed;
            _timer.AutoReset = true;
        }
        
        public Task StartAsync()
        {
            if (!_timer.Enabled)
                _timer.Enabled = true;

            return Task.CompletedTask;
        }

        public Task StartAsync(int interval)
        {
            if (!_timer.Enabled)
            {
                _timer.Interval = interval;
                _timer.Enabled = true;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _timer.Enabled = false;

            return Task.CompletedTask;
        }

        private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            Changed?.Invoke(this, 5_000);
        }
    }
}
