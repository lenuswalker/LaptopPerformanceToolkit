using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

public class DeactivateGPUAutomationStep(DeactivateGPUAutomationStepState state)
    : IAutomationStep<DeactivateGPUAutomationStepState>
{
    private readonly GPUController _controller = IoCContainer.Resolve<GPUController>();

    public DeactivateGPUAutomationStepState State { get; } = state;

    public Task<DeactivateGPUAutomationStepState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<DeactivateGPUAutomationStepState>());

    public Task<bool> IsSupportedAsync() => Task.FromResult(_controller.IsSupported());

    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        if (!_controller.IsSupported())
            return;

        var status = await _controller.RefreshNowAsync(token).ConfigureAwait(false);

        switch (State)
        {
            case DeactivateGPUAutomationStepState.KillApps when status.State is GPUState.Active:
                await _controller.KillGPUProcessesAsync(token).ConfigureAwait(false);
                break;
            case DeactivateGPUAutomationStepState.RestartGPU when status.State is GPUState.Active or GPUState.Inactive:
                var result = await _controller.RestartGPUAsync(token: token).ConfigureAwait(false);
                if (result is GPURestartResult.Failed or GPURestartResult.TimedOut)
                    throw new InvalidOperationException($"GPU restart failed: {result}.");
                break;
        }
    }

    IAutomationStep IAutomationStep.DeepCopy() => new DeactivateGPUAutomationStep(State);
}
