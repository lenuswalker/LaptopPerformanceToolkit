using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard
{
    public class DisplayBrightnessControl : AbstractSliderFeatureCardControl<int>
    {
        protected override int Value => 0;

        protected override int Maximum => 100;

        public DisplayBrightnessControl()
        {
            Icon = SymbolRegular.BrightnessHigh24;
            Title = Resource.DisplayBrightnessControl_Title;
            Subtitle = Resource.DisplayBrightnessControl_Message;
        }
    }
}
