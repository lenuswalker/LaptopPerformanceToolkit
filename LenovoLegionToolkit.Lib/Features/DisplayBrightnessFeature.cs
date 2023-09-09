using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using WindowsDisplayAPI;

namespace LenovoLegionToolkit.Lib.Features;

public class DisplayBrightnessFeature : IFeature<int>
{
    public async Task<bool> IsSupportedAsync()
    {
        var display = await GetBuiltInDisplayAsync().ConfigureAwait(false);
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found. Disabling {nameof(DisplayBrightnessFeature)}");

            return false;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Built in display found. Enabling {nameof(DisplayBrightnessFeature)}");

        return true;
    }
    
    public async Task<int[]> GetAllStatesAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting possible display brightness values...");

        var display = await GetBuiltInDisplayAsync().ConfigureAwait(false);
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");

            return new int[0];
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Built in display found: {display}");

        var currentSettings = await GetStateAsync().ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current built in display settings: {currentSettings}");

        var brightness = currentSettings;
        int low = 0;
        int high = 100;
        
        if (brightness >= low && brightness <= high)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Display brightness is in range");

            int[] possibleValues = Enumerable.Repeat(low, high).ToArray();
            return possibleValues;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Display brightness is not in range");

        return new int[0];
    }
    
    public async Task<int> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting current display brightness...");

        var display = await GetBuiltInDisplayAsync().ConfigureAwait(false);
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");

            return default;
        }

        var currentSettings = display.CurrentSetting;
        var brightness = await WMI.CallAsync("root\\WMI",
            $"SELECT * FROM WmiMonitorBrightness",
            $"CurrentBrightness");

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current display brightness is {brightness} [currentSettings={currentSettings}]");

        return Convert.ToInt32(brightness);
    }

    public async Task SetStateAsync(int brightness)
    {
        var display = await GetBuiltInDisplayAsync().ConfigureAwait(false);
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");
            throw new InvalidOperationException("Built in display not found");
        }

        var currentSettings = await GetStateAsync().ConfigureAwait(false);

        if (currentSettings == brightness)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Display brightness already set to {brightness}");
            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting display brightness to {brightness}");

        await WMI.CallAsync(@"root\WMI",
            $"SELECT * FROM WmiMonitorBrightnessMethods",
            "WmiSetBrightness",
            new() { { "Timeout", 1 }, { "Brightness", brightness } });
    }

    private static async Task<Display?> GetBuiltInDisplayAsync()
    {
        var displays = Display.GetDisplays();

        if (Log.Instance.IsTraceEnabled)
        {
            Log.Instance.Trace($"Found displays:");
            foreach (var display in displays)
                Log.Instance.Trace($" - {display}");
        }

        foreach (var display in Display.GetDisplays())
            if (await IsInternalAsync(display).ConfigureAwait(false))
                return display;
        return null;
    }

    private static async Task<bool> IsInternalAsync(Display display)
    {
        var instanceName = display.DevicePath
            .Split("#")
            .Skip(1)
            .Take(2)
            .Aggregate((s1, s2) => s1 + "\\" + s2);

        var result = await WMI.ReadAsync("root\\WMI",
                         $"SELECT * FROM WmiMonitorConnectionParams WHERE InstanceName LIKE '%{instanceName}%'",
                         pdc => (uint)pdc["VideoOutputTechnology"].Value).ConfigureAwait(false);
        var vot = result.FirstOrDefault();

        const uint votInternal = 0x80000000;
        const uint votDisplayPortEmbedded = 11;
        return vot == votInternal || vot == votDisplayPortEmbedded;
    }
}
