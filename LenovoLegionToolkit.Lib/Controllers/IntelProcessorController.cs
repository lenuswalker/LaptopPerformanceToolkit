using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Controllers;

public class IntelProcessorController : ProcessorController
{
    private readonly KX _kx = new();

    public override bool SupportsStapm => false;
    public override bool SupportsMSR => true;
    public override string FastLimitDisplayName => "PL2";
    public override string SlowLimitDisplayName => "PL1";

    protected override Task<bool> InitializeAsync() => _kx.InitAsync();

    public override async Task<Dictionary<PowerType, int>> GetTDPLimitsAsync()
    {
        var limits = new Dictionary<PowerType, int>();

        if (!await IsSupportedAsync().ConfigureAwait(false))
            return limits;

        using (await IoLock.LockAsync().ConfigureAwait(false))
        {
            var fast = await _kx.GetTDPLimitAsync(PowerType.Fast).ConfigureAwait(false);
            if (fast > 0)
                limits[PowerType.Fast] = fast;

            var slow = await _kx.GetTDPLimitAsync(PowerType.Slow).ConfigureAwait(false);
            if (slow > 0)
                limits[PowerType.Slow] = slow;
        }

        return limits;
    }

    public override async Task SetTDPLimitAsync(PowerType type, int watts)
    {
        if (watts <= 0 || type == PowerType.Stapm)
            return;

        if (!await IsSupportedAsync().ConfigureAwait(false))
            return;

        using (await IoLock.LockAsync().ConfigureAwait(false))
        {
            var result = await _kx.SetTDPLimitAsync(type, watts).ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Set {type} TDP limit to {watts} W. [result={result}]");
        }
    }

    public override async Task SetMSRLimitsAsync(int pl1, int pl2)
    {
        if (pl1 <= 0 || pl2 <= 0)
            return;

        if (!await IsSupportedAsync().ConfigureAwait(false))
            return;

        using (await IoLock.LockAsync().ConfigureAwait(false))
        {
            var result = await _kx.SetMSRLimitsAsync(pl1, pl2).ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Set MSR limits PL1={pl1} W, PL2={pl2} W. [result={result}]");
        }
    }
}
