using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Automation.Utils;
using LenovoLegionToolkit.Lib.Automation.Steps;

namespace LenovoLegionToolkit.WPF.Utils
{
    public static class PowerMode
    {
        /// <summary>
        /// Better Battery mode.
        /// </summary>
        public static Guid BetterBattery = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");

        /// <summary>
        /// Better Performance mode.
        /// </summary>
        // public static Guid BetterPerformance = new Guid("3af9B8d9-7c97-431d-ad78-34a8bfea439f");
        public static Guid BetterPerformance = new Guid("00000000-0000-0000-0000-000000000000");

        /// <summary>
        /// Best Performance mode.
        /// </summary>
        public static Guid BestPerformance = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");

        public static List<Guid> PowerModes = new() { BetterBattery, BetterPerformance, BestPerformance };
    }
    
    public class ProcessorManager
    {
        #region imports
        /// <summary>
        /// Retrieves the active overlay power scheme and returns a GUID that identifies the scheme.
        /// </summary>
        /// <param name="EffectiveOverlayPolicyGuid">A pointer to a GUID structure.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerGetEffectiveOverlayScheme")]
        private static extern uint PowerGetEffectiveOverlayScheme(out Guid EffectiveOverlayPolicyGuid);

        /// <summary>
        /// Sets the active power overlay power scheme.
        /// </summary>
        /// <param name="OverlaySchemeGuid">The identifier of the overlay power scheme.</param>
        /// <returns>Returns zero if the call was successful, and a nonzero value if the call failed.</returns>
        [DllImportAttribute("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
        private static extern uint PowerSetActiveOverlayScheme(Guid OverlaySchemeGuid);
        #endregion

        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();

        // timers
        //private Timer powerWatchdog;

        private Timer cpuWatchdog;
        protected object cpuLock = new();

        //private Timer gfxWatchdog;
        //protected object gfxLock = new();

        // GPU limits
        //private double FallbackGfxClock;
        //private double StoredGfxClock;
        //private double CurrentGfxClock;

        // Power modes
        private Guid RequestedPowerMode;

        private readonly ProcessorSettings _settings;
        private readonly AutomationSettings _automationSettings;
        
        AutomationPipeline ACPipeline = new();
        AutomationPipeline DCPipeline = new();
        List<AutomationPipeline> automationPipelines = new();


        public ProcessorManager(ProcessorSettings settings, AutomationSettings automationSettings)
        {
            _settings = settings;
            _automationSettings = automationSettings;

            // initialize timer(s)
            cpuWatchdog = new Timer() { Interval = 3000, AutoReset = true, Enabled = false };
            cpuWatchdog.Elapsed += cpuWatchdog_Elapsed;

            //gfxWatchdog = new Timer() { Interval = 3000, AutoReset = true, Enabled = false };
            //gfxWatchdog.Elapsed += gfxWatchdog_Elapsed;

            // initialize processor
            _controller = _controller.GetCurrent();

            cpuWatchdog.Start();
            //gfxWatchdog.Start();  
        }

        private void powerWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            // Checking if active power shceme has changed
            if (PowerGetEffectiveOverlayScheme(out Guid activeScheme) == 0)
                if (activeScheme != RequestedPowerMode)
                    PowerSetActiveOverlayScheme(RequestedPowerMode);
        }

        private async void cpuWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        {
            PowerAdapterStatus powerAdapterStatus = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
            lock (cpuLock)
            {
                Dictionary<PowerType, int> currentLimits = new();
                Dictionary<PowerType, int> currentMSRLimits = new();
                Dictionary<PowerType, int> savedLimits = new();
                foreach (PowerType type in Enum.GetValues(typeof(PowerType)))
                {
                    if (type == PowerType.Stapm || type == PowerType.Fast || type == PowerType.Slow)
                    {
                        int limit = _controller.GetTDPLimit(type);
                        currentLimits.Add(type, limit);
                    }
                }
                
                if (Environment.GetEnvironmentVariable("GameMode", EnvironmentVariableTarget.Machine) == "1")
                {
                    savedLimits = new()
                    {
                        {
                            PowerType.Stapm,
                            (int)_settings.Store.State.Mode[TDPMode.GameMode].Stapm
                        },
                        {
                            PowerType.Fast,
                            (int)_settings.Store.State.Mode[TDPMode.GameMode].Fast
                        },
                        {
                            PowerType.Slow,
                            (int)_settings.Store.State.Mode[TDPMode.GameMode].Slow
                        }
                    };
                    cpuWatchdog.Interval = 3000;
                }
                else
                {
                    automationPipelines = _automationSettings.Store.Pipelines;
                    foreach (var pipeline in automationPipelines)
                    {
                        if(pipeline.Trigger == null) 
                            continue;

                        if (pipeline.Trigger.DisplayName == "When on AC power")
                            ACPipeline = pipeline;
                        if (pipeline.Trigger.DisplayName == "When on battery power")
                            DCPipeline = pipeline;
                    }
                    
                    if (powerAdapterStatus == PowerAdapterStatus.Connected)
                    {
                        if (ACPipeline != null)
                        {
                            foreach (var step in ACPipeline.Steps)
                            {
                                if (step.GetType() == typeof(ProcessorTDPAutomationStep))
                                {
                                    ProcessorTDPAutomationStep processorTDPAutomationStep = (ProcessorTDPAutomationStep)step;
                                    savedLimits = new()
                                    {
                                        {
                                            PowerType.Stapm,
                                            (int)processorTDPAutomationStep.Stapm
                                        },
                                        {
                                            PowerType.Fast,
                                            (int)processorTDPAutomationStep.Fast
                                        },
                                        {
                                            PowerType.Slow,
                                            (int)processorTDPAutomationStep.Slow
                                        }
                                    };
                                }
                            }
                        }
                        else
                        {
                            savedLimits = new()
                            {
                                {
                                    PowerType.Stapm,
                                    (int)_settings.Store.State.Mode[TDPMode.AC].Stapm
                                },
                                {
                                    PowerType.Fast,
                                    (int)_settings.Store.State.Mode[TDPMode.AC].Fast
                                },
                                {
                                    PowerType.Slow,
                                    (int)_settings.Store.State.Mode[TDPMode.AC].Slow
                                }
                            };
                        }
                        cpuWatchdog.Interval = 3000;
                    }
                    else
                    {
                        if (DCPipeline != null)
                        {
                            foreach (var step in DCPipeline.Steps)
                            {
                                if (step.GetType() == typeof(ProcessorTDPAutomationStep))
                                {
                                    ProcessorTDPAutomationStep processorTDPAutomationStep = (ProcessorTDPAutomationStep)step;
                                    savedLimits = new()
                                    {
                                        {
                                            PowerType.Stapm,
                                            (int)processorTDPAutomationStep.Stapm
                                        },
                                        {
                                            PowerType.Fast,
                                            (int)processorTDPAutomationStep.Fast
                                        },
                                        {
                                            PowerType.Slow,
                                            (int)processorTDPAutomationStep.Slow
                                        }
                                    };
                                }
                            }
                        }
                        else
                        {
                            savedLimits = new()
                            {
                                {
                                    PowerType.Stapm,
                                    (int)_settings.Store.State.Mode[TDPMode.DC].Stapm
                                },
                                {
                                    PowerType.Fast,
                                    (int)_settings.Store.State.Mode[TDPMode.DC].Fast
                                },
                                {
                                    PowerType.Slow,
                                    (int)_settings.Store.State.Mode[TDPMode.DC].Slow
                                }
                            };
                        }
                        cpuWatchdog.Interval = 15000;
                    }
                }

                // search for limit changes
                if (currentLimits.Any())
                    foreach (KeyValuePair<PowerType, int> pair in currentLimits)
                    {
                        if (!savedLimits.ContainsKey(pair.Key))
                            continue;

                        if (savedLimits[pair.Key] == pair.Value)
                            continue;

                        if (_controller.GetType() == typeof(AMDProcessorController))
                        {
                            // AMD reduces TDP by 10% when OS power mode is set to Best power efficiency
                            if (RequestedPowerMode == PowerMode.BetterBattery)
                                savedLimits[pair.Key] = (int)Math.Truncate(savedLimits[pair.Key] * 0.9);
                        }
                        else if (_controller.GetType() == typeof(IntelProcessorController))
                        {
                            // Intel doesn't have stapm
                            if (pair.Key == PowerType.Stapm)
                                continue;
                        }

                        _controller.SetTDPLimit(pair.Key, savedLimits[pair.Key]);
                    }

                // processor specific
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
        }

        //private void gfxWatchdog_Elapsed(object? sender, ElapsedEventArgs e)
        //{
        //    lock (gfxLock)
        //    {
        //        if (_controller.GetType() == typeof(AMDProcessorController))
        //        {
        //            // not ready yet
        //            if (CurrentGfxClock == 0)
        //                return;
        //        }
        //        else if (_controller.GetType() == typeof(IntelProcessorController))
        //        {
        //            // not ready yet
        //            if (CurrentGfxClock == 12750)
        //                return;
        //        }

        //        // not ready yet
        //        if (StoredGfxClock == 0)
        //            return;

        //        // only request an update if current gfx clock is different than stored
        //        if (CurrentGfxClock != StoredGfxClock)
        //            _controller.SetGPUClock(StoredGfxClock);
        //    }
        //}

        //public void RequestGPUClock(double value, bool UserRequested = true)
        //{
        //    if (UserRequested)
        //        FallbackGfxClock = value;

        //    // update value read by timer
        //    StoredGfxClock = value;
        //}

        //public void RequestPowerMode(int idx)
        //{
        //    RequestedPowerMode = PowerMode.PowerModes[idx];
        //    LogManager.LogInformation("User requested power scheme: {0}", RequestedPowerMode);

        //    PowerSetActiveOverlayScheme(RequestedPowerMode);
        //}
    }
}
