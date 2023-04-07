using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;

namespace LenovoLegionToolkit.Lib.Controllers;

public class IntelProcessorController : ProcessorController
{
    public KX platform = new KX();

    public string? family;

    public IntelProcessorController() : base()
    {
        IsInitialized = platform.init();
        if (IsInitialized)
        {
            family = ProcessorID.Substring(ProcessorID.Length - 5);

            switch (family)
            {
                default:
                case "206A7": // SandyBridge
                case "306A9": // IvyBridge
                case "40651": // Haswell
                case "306D4": // Broadwell
                case "406E3": // Skylake
                case "906ED": // CoffeeLake
                case "806E9": // AmberLake
                case "706E5": // IceLake
                case "806C1": // TigerLake U
                case "806C2": // TigerLake U Refresh
                case "806D1": // TigerLake H
                case "906A2": // AlderLake-P
                case "906A3": // AlderLake-P
                case "906A4": // AlderLake-P
                case "90672": // AlderLake-S
                case "90675": // AlderLake-S
                    CanChangeTDP = true;
                    CanChangeGPU = true;
                    break;
            }

            foreach (PowerType type in (PowerType[])Enum.GetValues(typeof(PowerType)))
            {
                // write default limits
                m_Limits[type] = 0;
                m_PrevLimits[type] = 0;
            }
        }
    }

    public override void Initialize()
    {
        updateTimer.Elapsed += UpdateTimer_Elapsed;
        base.Initialize();
    }

    public override void Stop()
    {
        updateTimer.Elapsed -= UpdateTimer_Elapsed;
        base.Stop();
    }

    protected override void UpdateTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        lock (base.IsBusy)
        {
            // read limit(s)
            int limit_short = (int)platform.get_short_limit(false);
            int limit_long = (int)platform.get_long_limit(false);

            if (limit_short != -1)
                base.m_Limits[PowerType.Fast] = limit_short;
            if (limit_long != -1)
                base.m_Limits[PowerType.Slow] = limit_long;

            // read msr limit(s)
            int msr_short = (int)platform.get_short_limit(true);
            int msr_long = (int)platform.get_long_limit(true);

            if (msr_short != -1)
                base.m_Limits[PowerType.Fast] = msr_short;
            if (msr_long != -1)
                base.m_Limits[PowerType.Slow] = msr_long;

            // read gfx_clk
            int gfx_clk = (int)platform.get_gfx_clk();

            if (gfx_clk != -1)
                base.m_Misc["gfx_clk"] = gfx_clk;

            base.UpdateTimer_Elapsed(sender, e);
        }
    }

    public override void SetTDPLimit(PowerType type, double limit, int result)
    {
        if (limit == 0)
            return;

        lock (base.IsBusy)
        {
            var error = 0;

            switch (type)
            {
                case PowerType.Slow:
                    error = platform.set_long_limit((int)limit, false);
                    break;
                case PowerType.Fast:
                    error = platform.set_short_limit((int)limit, false);
                    break;
            }
                
            base.SetTDPLimit(type, limit, error);
        }
    }

    public override int GetTDPLimit(PowerType type)
    {
            
        lock (base.IsBusy)
        {
            int limit = 0;

            switch (type)
            {
                case PowerType.Fast:
                    limit = (int)platform.get_short_limit(false);
                    break;
                case PowerType.Slow:
                    limit = (int)platform.get_long_limit(false);
                    break;
            }
            return limit;
        }
    }

    public override Task<int> GetTDPLimitAsync(PowerType type)
    {

        lock (base.IsBusy)
        {
            int limit = 0;

            switch (type)
            {
                case PowerType.Fast:
                    limit = (int)platform.get_short_limit(false);
                    break;
                case PowerType.Slow:
                    limit = (int)platform.get_long_limit(false);
                    break;
            }
            return Task.FromResult(limit);
        }
    }

    public override Task<Dictionary<PowerType, int>> GetTDPLimitsAsync()
    {

        lock (base.IsBusy)
        {
            Dictionary<PowerType, int> limits = new();
            int limit = 0;

            limit = platform.get_short_limit(false);
            limits.Add(PowerType.Fast, limit);

            limit = platform.get_long_limit(false);
            limits.Add(PowerType.Slow, limit);
            
            return Task.FromResult(limits);
        }
    }

    public void SetMSRLimits(double PL1, double PL2)
    {
        if (PL1 == 0)
            return;

        if (PL2 == 0)
            return;

        lock (base.IsBusy)
        {
            var error = 0;

            error = platform.set_msr_limits((int)PL1, (int)PL2);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"User requested MSR limits PL1: {PL1} & PL2: {PL2}, error code: {error}");
        }
    }
        
    public Dictionary<PowerType, int> GetMSRLimits()
    {
        lock (base.IsBusy)
        {
            Dictionary<PowerType, int> limits = new Dictionary<PowerType, int>
            {
                { PowerType.Fast, platform.get_short_limit(true) },
                { PowerType.Slow, platform.get_long_limit(true) }
            };
            return limits;
        }
    }

    public override void SetGPUClock(double clock, int result)
    {
        lock (base.IsBusy)
        {
            var error = platform.set_gfx_clk((int)clock);

            base.SetGPUClock(clock, error);
        }
    }
}
