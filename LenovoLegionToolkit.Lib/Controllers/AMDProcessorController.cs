using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Controllers;

public class AMDProcessorController : ProcessorController
{
    private IntPtr _ry = IntPtr.Zero;

    public override bool SupportsStapm => true;
    public override bool SupportsMSR => false;
    public override string FastLimitDisplayName => "Fast";
    public override string SlowLimitDisplayName => "Slow";

    protected override Task<bool> InitializeAsync() => Task.Run(() =>
    {
        try
        {
            _ry = RyzenAdj.init_ryzenadj();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to load libryzenadj.", ex);

            return false;
        }

        if (_ry == IntPtr.Zero)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"RyzenAdj initialization failed.");

            return false;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"RyzenAdj initialized. [family={RyzenAdj.get_cpu_family(_ry)}]");

        return true;
    });

    public override async Task<Dictionary<PowerType, int>> GetTDPLimitsAsync()
    {
        var limits = new Dictionary<PowerType, int>();

        if (!await IsSupportedAsync().ConfigureAwait(false))
            return limits;

        using (await IoLock.LockAsync().ConfigureAwait(false))
        {
            await Task.Run(() =>
            {
                var result = RyzenAdj.refresh_table(_ry);
                if (result != 0)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"RyzenAdj table refresh failed. [result={result}]");

                    return;
                }

                AddIfValid(limits, PowerType.Stapm, RyzenAdj.get_stapm_limit(_ry));
                AddIfValid(limits, PowerType.Fast, RyzenAdj.get_fast_limit(_ry));
                AddIfValid(limits, PowerType.Slow, RyzenAdj.get_slow_limit(_ry));
            }).ConfigureAwait(false);
        }

        return limits;
    }

    public override async Task SetTDPLimitAsync(PowerType type, int watts)
    {
        if (watts <= 0)
            return;

        if (!await IsSupportedAsync().ConfigureAwait(false))
            return;

        using (await IoLock.LockAsync().ConfigureAwait(false))
        {
            await Task.Run(() =>
            {
                var milliwatts = (uint)watts * 1000;
                var result = type switch
                {
                    PowerType.Stapm => RyzenAdj.set_stapm_limit(_ry, milliwatts),
                    PowerType.Fast => RyzenAdj.set_fast_limit(_ry, milliwatts),
                    PowerType.Slow => RyzenAdj.set_slow_limit(_ry, milliwatts),
                    _ => -1
                };

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Set {type} TDP limit to {watts} W. [result={result}]");
            }).ConfigureAwait(false);
        }
    }

    private static void AddIfValid(Dictionary<PowerType, int> limits, PowerType type, float value)
    {
        if (!float.IsNaN(value) && value > 0)
            limits[type] = (int)Math.Round(value);
    }
}
