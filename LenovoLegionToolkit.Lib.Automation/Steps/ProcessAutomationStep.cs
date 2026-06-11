using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

public class ProcessAutomationStep : IAutomationStep
{
    public ProcessAutomationState State { get; }

    [JsonConstructor]
    public ProcessAutomationStep(ProcessAutomationState state) => State = state;

    public Task<ProcessState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<ProcessState>());

    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        if (State.Processes is null)
            return;

        switch (State.State)
        {
            case ProcessState.Start:
                foreach (var process in State.Processes)
                {
                    if (string.IsNullOrEmpty(process.ExecutablePath))
                        continue;

                    await CMD.RunAsync(process.ExecutablePath,
                        string.Empty,
                        useShellExecute: true,
                        createNoWindow: true,
                        waitForExit: false,
                        token).ConfigureAwait(false);
                }
                break;
            case ProcessState.Stop:
                foreach (var process in State.Processes)
                {
                    foreach (var p in Process.GetProcessesByName(process.Name))
                    {
                        using (p)
                        {
                            try
                            {
                                p.Kill();
                            }
                            catch { /* Already exited or access denied. */ }
                        }
                    }
                }
                break;
        }
    }

    public IAutomationStep DeepCopy() => new ProcessAutomationStep(State);
}
