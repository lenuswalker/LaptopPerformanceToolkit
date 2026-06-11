using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Features;

// Controls the built-in display backlight via WMI. Only internal panels
// expose WmiMonitorBrightness, so its presence doubles as the support check.
public class DisplayBrightnessFeature : IFeature<int>
{
    public Task<bool> IsSupportedAsync() => WMI.WmiMonitorBrightness.ExistsAsync();

    public Task<int[]> GetAllStatesAsync() => Task.FromResult(Enumerable.Range(0, 101).ToArray());

    public Task<int> GetStateAsync() => WMI.WmiMonitorBrightness.GetCurrentBrightnessAsync();

    public Task SetStateAsync(int state) => WMI.WmiMonitorBrightnessMethods.WmiSetBrightness(state, 1);
}
