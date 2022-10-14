using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps
{
    public class ProcessorTDPAutomationStep : IAutomationStep
    {
        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();
        public double Stapm { get; }
        public double Fast { get; }
        public double Slow { get; }
        public bool? UseMSR { get; }

        [JsonConstructor]
        public ProcessorTDPAutomationStep(double stapm, double fast, double slow, bool? useMSR)
        {
            Stapm = stapm;
            Fast = fast;
            Slow = slow;
            UseMSR = useMSR;
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
            if (UseMSR != null)
                if ((bool)UseMSR)
                {
                    if (processor.GetType() == typeof(IntelProcessorController))
                    {
                        ((IntelProcessorController)processor).SetMSRLimits(Slow, Fast);
                    }
                }
        }

        IAutomationStep IAutomationStep.DeepCopy() => new ProcessorTDPAutomationStep(Stapm, Fast, Slow, UseMSR);
    }
}
