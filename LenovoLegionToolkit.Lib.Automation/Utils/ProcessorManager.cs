using LenovoLegionToolkit.Lib.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Automation.Steps;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Automation.Utils
{
    public class ProcessorManager
    {
        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();

        // CPU limits
        private Dictionary<PowerType, int> currentLimits = new();
        private Dictionary<PowerType, int> currentMSRLimits = new();
        private Dictionary<PowerType, int> savedLimits = new();

        private readonly ProcessorSettings _settings;
        private readonly AutomationSettings _automationSettings;

        private AutomationPipeline _acPipeline = new();
        private AutomationPipeline _dcPipeline = new();
        private List<AutomationPipeline> _pipelines = new();


        public ProcessorManager(ProcessorSettings settings, AutomationSettings automationSettings)
        {
            _settings = settings;
            _automationSettings = automationSettings;

            // initialize processor
            _controller = _controller.GetCurrent();
        }

        public bool IsSupported()
        {
            return true;
        }

        public Task MaintainTDP()
        {
            PowerAdapterStatus powerAdapterStatus = Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            _settings.SynchronizeStore();
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
            }
            else
            {
                _pipelines = _automationSettings.Store.Pipelines;
                foreach (var pipeline in _pipelines)
                {
                    if (pipeline.Trigger == null)
                        continue;

                    if (pipeline.Trigger.DisplayName == "When on AC power")
                        _acPipeline = pipeline;
                    if (pipeline.Trigger.DisplayName == "When on battery power")
                        _dcPipeline = pipeline;
                }

                if (powerAdapterStatus == PowerAdapterStatus.Connected)
                {
                    if (_acPipeline != null)
                    {
                        foreach (var step in _acPipeline.Steps)
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
                }
                else
                {
                    if (_dcPipeline != null)
                    {
                        foreach (var step in _dcPipeline.Steps)
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
                }
            }

            // get current limits
            foreach (PowerType type in Enum.GetValues(typeof(PowerType)))
            {
                if (type == PowerType.Stapm || type == PowerType.Fast || type == PowerType.Slow)
                {
                    if (_controller.GetType() == typeof(IntelProcessorController))
                    {
                        // Intel doesn't have stapm
                        if (type == PowerType.Stapm)
                            continue;
                    }

                    int limit = _controller.GetTDPLimit(type);
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

            savedLimits.Clear();
            currentLimits.Clear();
            currentMSRLimits.Clear();

            return Task.CompletedTask;
        }
    }
}
