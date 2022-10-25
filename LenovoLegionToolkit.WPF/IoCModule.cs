﻿using Autofac;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.WPF.Utils;

namespace LenovoLegionToolkit.WPF
{
    public class IoCModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register<ThemeManager>();
            builder.Register<SystemAccentColorHelper>();

            builder.Register<NotificationsManager>().AutoActivate();
        }
    }
}
