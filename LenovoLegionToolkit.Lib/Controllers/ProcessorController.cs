using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.Controllers;

public abstract class ProcessorController
{
    private readonly AsyncLock _initLock = new();

    private bool? _isSupported;

    protected readonly AsyncLock IoLock = new();

    public abstract bool SupportsStapm { get; }
    public abstract bool SupportsMSR { get; }
    public abstract string FastLimitDisplayName { get; }
    public abstract string SlowLimitDisplayName { get; }

    public async Task<bool> IsSupportedAsync()
    {
        using (await _initLock.LockAsync().ConfigureAwait(false))
            return _isSupported ??= await InitializeAsync().ConfigureAwait(false);
    }

    public async Task<ProcessorTDPState> GetProcessorTDPAsync()
    {
        var limits = await GetTDPLimitsAsync().ConfigureAwait(false);

        return new ProcessorTDPState
        {
            Stapm = limits.GetValueOrDefault(PowerType.Stapm),
            Fast = limits.GetValueOrDefault(PowerType.Fast),
            Slow = limits.GetValueOrDefault(PowerType.Slow)
        };
    }

    public abstract Task<Dictionary<PowerType, int>> GetTDPLimitsAsync();

    public abstract Task SetTDPLimitAsync(PowerType type, int watts);

    public virtual Task SetMSRLimitsAsync(int pl1, int pl2) => Task.CompletedTask;

    protected abstract Task<bool> InitializeAsync();

    public static ProcessorController Create()
    {
        var vendor = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "VendorIdentifier", null) as string;
        return vendor == "AuthenticAMD"
            ? new AMDProcessorController()
            : new IntelProcessorController();
    }
}
