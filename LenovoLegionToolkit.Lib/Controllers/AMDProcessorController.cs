using LenovoLegionToolkit.Lib.System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;

namespace LenovoLegionToolkit.Lib.Controllers;

public class AMDProcessorController : ProcessorController
{
    public IntPtr ry;
    public RyzenFamily family;

    public AMDProcessorController() : base()
    {
        ry = RyzenAdj.init_ryzenadj();

        if (ry == IntPtr.Zero)
            IsInitialized = false;
        else
        {
            family = RyzenAdj.get_cpu_family(ry);
            IsInitialized = true;

            switch (family)
            {
                default:
                    CanChangeGPU = false;
                    break;

                case RyzenFamily.FAM_RENOIR:
                case RyzenFamily.FAM_LUCIENNE:
                case RyzenFamily.FAM_CEZANNE:
                case RyzenFamily.FAM_VANGOGH:
                case RyzenFamily.FAM_REMBRANDT:
                    CanChangeGPU = true;
                    break;
            }

            switch (family)
            {
                default:
                    CanChangeTDP = false;
                    break;

                case RyzenFamily.FAM_RAVEN:
                case RyzenFamily.FAM_PICASSO:
                case RyzenFamily.FAM_DALI:
                case RyzenFamily.FAM_RENOIR:
                case RyzenFamily.FAM_LUCIENNE:
                case RyzenFamily.FAM_CEZANNE:
                case RyzenFamily.FAM_VANGOGH:
                case RyzenFamily.FAM_REMBRANDT:
                    CanChangeTDP = true;
                    break;
            }
        }

        foreach (PowerType type in (PowerType[])Enum.GetValues(typeof(PowerType)))
        {
            // write default limits
            m_Limits[type] = 0;
            m_PrevLimits[type] = 0;
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
            RyzenAdj.get_table_values(ry);
            RyzenAdj.refresh_table(ry);

            // read limit(s)
            int limit_fast = (int)RyzenAdj.get_fast_limit(ry);
            int limit_slow = (int)RyzenAdj.get_slow_limit(ry);
            int limit_stapm = (int)RyzenAdj.get_stapm_limit(ry);

            if (limit_fast != 0)
                base.m_Limits[PowerType.Fast] = limit_fast;
            if (limit_slow != 0)
                base.m_Limits[PowerType.Slow] = limit_slow;
            if (limit_stapm != 0)
                base.m_Limits[PowerType.Stapm] = limit_stapm;

            // read gfx_clk
            int gfx_clk = (int)RyzenAdj.get_gfx_clk(ry);
            if (gfx_clk != 0)
                base.m_Misc["gfx_clk"] = gfx_clk;

            base.UpdateTimer_Elapsed(sender, e);
        }
    }

    public override void SetTDPLimit(PowerType type, double limit, int result)
    {
        if (ry == IntPtr.Zero)
            return;

        if (limit == 0)
            return;

        lock (base.IsBusy)
        {
            // 15W : 15000
            limit *= 1000;

            var error = 0;

            switch (type)
            {
                case PowerType.Fast:
                    error = RyzenAdj.set_fast_limit(ry, (uint)limit);
                    break;
                case PowerType.Slow:
                    error = RyzenAdj.set_slow_limit(ry, (uint)limit);
                    break;
                case PowerType.Stapm:
                    error = RyzenAdj.set_stapm_limit(ry, (uint)limit);
                    break;
            }
            
            base.SetTDPLimit(type, limit, error);
        }
    }

    public override int GetTDPLimit(PowerType type)
    {
        if (ry == IntPtr.Zero)
            return 0;

        lock (base.IsBusy)
        {
            int limit = 0;

            switch (type)
            {
                case PowerType.Fast:
                    limit = (int)RyzenAdj.get_fast_limit(ry);
                    break;
                case PowerType.Slow:
                    limit = (int)RyzenAdj.get_slow_limit(ry);
                    break;
                case PowerType.Stapm:
                    limit = (int)RyzenAdj.get_stapm_limit(ry);
                    break;
            }
            return limit;
        }
    }

    public override Task<int> GetTDPLimitAsync(PowerType type)
    {
        if (ry == IntPtr.Zero)
            return Task.FromResult(0);

        lock (base.IsBusy)
        {
            int limit = 0;

            switch (type)
            {
                case PowerType.Fast:
                    limit = (int)RyzenAdj.get_fast_limit(ry);
                    break;
                case PowerType.Slow:
                    limit = (int)RyzenAdj.get_slow_limit(ry);
                    break;
                case PowerType.Stapm:
                    limit = (int)RyzenAdj.get_stapm_limit(ry);
                    break;
            }
            return Task.FromResult(limit);
        }
    }

    public override Task<Dictionary<PowerType, int>> GetTDPLimitsAsync()
    {
        if (ry == IntPtr.Zero)
            return Task.FromResult(new Dictionary<PowerType, int>());

        lock (base.IsBusy)
        {
            Dictionary<PowerType, int> limits = getSensorValuesRAdj();

            return Task.FromResult(limits);
        }
    }

    public static decimal getSensorValueRAdj(string SensorName)
    {
        using (Process process = new Process())
        {
            int i = 0;

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "AMD", "ryzenadj.exe");
            process.StartInfo.FileName = path;
            process.StartInfo.Arguments = "-i";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            // Synchronously read the standard output of the spawned process.
            StreamReader reader = process.StandardOutput;
            string output = reader.ReadToEnd();

            string[] lines = output.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            if (lines != null || lines.Length != 0)
            {
                do { i++; } while (!lines[i].Contains(SensorName));

                if (lines[i].Contains(SensorName))
                {
                    lines[i] = lines[i].Substring(25);
                    lines[i] = lines[i].Remove(lines[i].Length - 21);
                    lines[i] = lines[i].Replace("|", null);
                    lines[i] = lines[i].Replace(" ", null);

                    return Convert.ToDecimal(lines[i].ToString());
                }
                else
                {
                    return 0;
                }
            }
            process.WaitForExit();
        }

        return 0;
    }

    public static Dictionary<PowerType, int> getSensorValuesRAdj()
    {
        Dictionary<PowerType, int> limits = new();
        using (Process process = new Process())
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "AMD", "ryzenadj.exe");
            process.StartInfo.FileName = path;
            process.StartInfo.Arguments = "-i";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            // Synchronously read the standard output of the spawned process.
            StreamReader reader = process.StandardOutput;
            string output = reader.ReadToEnd();

            string[] lines = output.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            if (lines != null || lines.Length != 0)
            {
                foreach (string line in lines)
                {
                    if (line.Contains("PPT LIMIT FAST"))
                    {
                        string result = line;
                        result = result.Substring(25);
                        result = result.Remove(result.Length - 21);
                        result = result.Replace("|", null);
                        result = result.Replace(" ", null);

                        limits.Add(PowerType.Fast, (int)Convert.ToDecimal(result.ToString()));
                    }
                    if (line.Contains("PPT LIMIT SLOW"))
                    {
                        string result = line;
                        result = result.Substring(25);
                        result = result.Remove(result.Length - 21);
                        result = result.Replace("|", null);
                        result = result.Replace(" ", null);

                        limits.Add(PowerType.Slow, (int)Convert.ToDecimal(result.ToString()));
                    }
                }
            }
        }

        return limits;
    }

    public override void SetGPUClock(double clock, int result)
    {
        lock (base.IsBusy)
        {
            // reset default var
            if (clock == 12750)
                return;

            var error = RyzenAdj.set_gfx_clk(ry, (uint)clock);

            base.SetGPUClock(clock, error);
        }
    }
}
