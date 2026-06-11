using System;
using System.Linq;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    public static class WmiMonitorBrightness
    {
        public static Task<bool> ExistsAsync() => WMI.ExistsAsync("root\\WMI",
            $"SELECT * FROM WmiMonitorBrightness");

        public static async Task<int> GetCurrentBrightnessAsync()
        {
            var result = await ReadAsync("root\\WMI",
                $"SELECT * FROM WmiMonitorBrightness",
                pdc => Convert.ToInt32(pdc["CurrentBrightness"].Value)).ConfigureAwait(false);
            return result.FirstOrDefault();
        }
    }
}
