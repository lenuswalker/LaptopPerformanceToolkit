﻿using LenovoLegionToolkit.Lib.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Listeners;

namespace LenovoLegionToolkit.Lib.Automation.Utils
{
    public class ProcessorManager
    {
        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();
        private readonly TimeIntervalAutomationListener _timeIntervalListener;

        // CPU limits
        private Dictionary<PowerType, int> currentLimits = new();
        private Dictionary<PowerType, int> currentMSRLimits = new();
        private Dictionary<PowerType, int> savedLimits = new();

        private double _stapm;
        private double _fast;
        private double _slow;
        private bool _useMSR;

        public ProcessorManager(TimeIntervalAutomationListener timeIntervalListener)
        {
            _timeIntervalListener = timeIntervalListener ?? throw new ArgumentNullException(nameof(timeIntervalListener));
            // initialize processor
            _controller = _controller.GetCurrent();

            _timeIntervalListener.Changed += TimeIntervalListener_Changed;
        }

        public bool IsSupported()
        {
            // Need to add some logic here
            return true;
        }

        public Task StartAsync(double stapm, double fast, double slow, bool useMSR, int interval)
        {
            _stapm = stapm;
            _fast = fast;
            _slow = slow;
            _useMSR = useMSR;
            _timeIntervalListener.StartAsync(interval*1000);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _timeIntervalListener.StopAsync();
            _stapm = 0;
            _fast = 0;
            _slow = 0;
            _useMSR = false;
            return Task.CompletedTask;
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
                if (currentLimits.ContainsKey(type))
                    currentLimits[type] = limit;
                else
                    currentLimits.Add(type, limit);
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

            savedLimits.Clear();
            currentLimits.Clear();
            currentMSRLimits.Clear();

            return Task.CompletedTask;
        }

        private async void TimeIntervalListener_Changed(object? sender, int interval)
        {
            await MaintainTDP(_stapm, _fast, _slow, _useMSR).ConfigureAwait(false);
        }
    }
}
