using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps
{
    public class ProcessorTDPAutomationStep : IAutomationStep
    {
        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();
        public double Stapm { get; }
        public double Fast { get; }
        public double Slow { get; }

        [JsonConstructor]
        public ProcessorTDPAutomationStep(double stapm, double fast, double slow)
        {
            Stapm = stapm;
            Fast = fast;
            Slow = slow;
        }
        
        public Task<bool> IsSupportedAsync() => Task.FromResult(true);

        public async Task RunAsync()
        {
            ProcessorController processor = _controller.GetCurrent();
            if (Stapm != 0)
                processor.SetTDPLimit(PowerType.Stapm, Stapm);
            if (Fast != 0)
                processor.SetTDPLimit(PowerType.Fast, Fast);
            if (Slow != 0)
                processor.SetTDPLimit(PowerType.Slow, Slow);
        }

        IAutomationStep IAutomationStep.DeepCopy() => new ProcessorTDPAutomationStep(Stapm, Fast, Slow);
    }
}
