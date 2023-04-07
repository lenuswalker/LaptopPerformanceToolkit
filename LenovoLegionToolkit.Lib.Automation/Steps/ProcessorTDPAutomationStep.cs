using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Utils;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

public class ProcessorTDPAutomationStep : IAutomationStep
{
    private readonly ProcessorManager _manager = IoCContainer.Resolve<ProcessorManager>();

    public ProcessorTDPState State { get; }

    [JsonConstructor]
    public ProcessorTDPAutomationStep(ProcessorTDPState state) => State = state;

    public Task<PowerType[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<PowerType>());

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public async Task RunAsync()
    {
        await _manager.InitializeAsync();

        if (State.Stapm != 0)
            _manager.SetTDPLimit(PowerType.Stapm, State.Stapm);
        if (State.Fast != 0)
            _manager.SetTDPLimit(PowerType.Fast, State.Fast);
        if (State.Slow != 0)
            _manager.SetTDPLimit(PowerType.Slow, State.Slow);
        if (State.UseMSR)
            _manager.SetMSRLimits(State.Slow, State.Fast);
        if (State.MaintainTDP)
            await _manager.StartAsync(State.Stapm, State.Fast, State.Slow, State.UseMSR, State.Interval).ConfigureAwait(false);
        else
            await _manager.StopAsync().ConfigureAwait(false);
    }

    public IAutomationStep DeepCopy() => new ProcessorTDPAutomationStep(State);
}
