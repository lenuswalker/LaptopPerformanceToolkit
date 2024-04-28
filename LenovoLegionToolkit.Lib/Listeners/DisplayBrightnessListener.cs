﻿using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

public class DisplayBrightnessListener(PowerPlanController powerPlanController, ApplicationSettings settings)
    : AbstractWMIListener<DisplayBrightnessListener.ChangedEventArgs, Brightness, byte>(WMI.WmiMonitorBrightnessEvent.Listen)
{
    public class ChangedEventArgs(Brightness brightness) : EventArgs
    {
        public Brightness Brightness { get; } = brightness;
    }

    private readonly ThrottleLastDispatcher _dispatcher = new(TimeSpan.FromSeconds(2), nameof(DisplayBrightnessListener));

    protected override Brightness GetValue(byte value) => new(value);

    protected override ChangedEventArgs GetEventArgs(Brightness value) => new(value);

    protected override async Task OnChangedAsync(Brightness value) => await SynchronizeBrightnessAsync(value).ConfigureAwait(false);

    private async Task SynchronizeBrightnessAsync(Brightness value)
    {
        if (!settings.Store.SynchronizeBrightnessToAllPowerPlans)
            return;

        await _dispatcher.DispatchAsync(() =>
        {
            SetBrightnessForAllPowerPlans(value);
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

    private void SetBrightnessForAllPowerPlans(Brightness brightness)
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Setting brightness to {brightness.Value}...");

            var powerPlans = powerPlanController.GetPowerPlans(true, true);

            foreach (var powerPlan in powerPlans)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Modifying power plan {powerPlan.Name}... [powerPlan.Guid={powerPlan.Guid}, powerPlan.IsOverlay={powerPlan.IsOverlay}, brightness={brightness.Value}]");

                powerPlanController.SetPowerPlanParameter(powerPlan, brightness);
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Brightness set to {brightness.Value}.");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set brightness to {brightness.Value}.", ex);
        }
    }
}
