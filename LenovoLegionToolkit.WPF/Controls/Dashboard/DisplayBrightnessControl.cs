using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

public class DisplayBrightnessControl : AbstractSliderFeatureCardControl<int>
{
    public DisplayBrightnessControl()
    {
        Icon = SymbolRegular.BrightnessHigh24;
        Title = Resource.DisplayBrightnessControl_Title;
        Subtitle = Resource.DisplayBrightnessControl_Message;
    }
}
