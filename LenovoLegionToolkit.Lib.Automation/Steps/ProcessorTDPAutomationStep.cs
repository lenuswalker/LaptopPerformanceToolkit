using System.Threading;
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

    public Task<bool> IsSupportedAsync() => _manager.IsSupportedAsync();

    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token) => _manager.ApplyAsync(State);

    public IAutomationStep DeepCopy() => new ProcessorTDPAutomationStep(State);
}
