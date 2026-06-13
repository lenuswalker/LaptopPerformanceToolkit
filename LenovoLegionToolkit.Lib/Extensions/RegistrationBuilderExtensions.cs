using System;
using Autofac;
using Autofac.Builder;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Extensions;

public static class RegistrationBuilderExtensions
{
    public static void AutoActivateListener<T>(this IRegistrationBuilder<IListener<T>, ConcreteReflectionActivatorData, SingleRegistrationStyle> registration) where T : EventArgs
    {
        registration.OnActivating(e => e.Instance.StartAsync().AsValueTask()).AutoActivate();
    }

    /// <summary>
    /// Auto-activates the listener only on Lenovo-compatible hardware. On unsupported machines the
    /// listener is still registered (so it can be resolved on demand) but is never started, so it
    /// never subscribes to its Lenovo-only WMI/driver event source and consumes no resources.
    /// </summary>
    public static void AutoActivateLenovoListener<T>(this IRegistrationBuilder<IListener<T>, ConcreteReflectionActivatorData, SingleRegistrationStyle> registration) where T : EventArgs
    {
        if (!Compatibility.IsBasicCompatible)
            return;

        registration.OnActivating(e => e.Instance.StartAsync().AsValueTask()).AutoActivate();
    }
}
