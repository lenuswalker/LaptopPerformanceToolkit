using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace LenovoLegionToolkit.Lib.Controllers
{
    public class AMDProcessorController : ProcessorController
    {
        public IntPtr ry;
        public RyzenFamily family;

        public AMDProcessorController() : base()
        {
            ry = RyzenAdj.init_ryzenadj();

            if (ry != IntPtr.Zero)
            {
                family = RyzenAdj.get_cpu_family(ry);

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

        protected override void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
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
}
