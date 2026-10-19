using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

public class DisplayBrightnessControl : AbstractSliderFeatureCardControl<int>
{
    public DisplayBrightnessControl()
    {
        Icon = new SymbolIcon(SymbolRegular.BrightnessHigh24);
        Title = Resource.DisplayBrightnessControl_Title;
        Subtitle = Resource.DisplayBrightnessControl_Message;
    }
}
