using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

public class RGBKeyboardBacklightSpeedCardControl : AbstractComboBoxRGBKeyboardCardControl<RGBKeyboardBacklightSpeed>
{
    public RGBKeyboardBacklightSpeedCardControl()
    {
        Icon = new SymbolIcon(SymbolRegular.Keyboard24);
        Title = Resource.RGBKeyboardBacklightSpeedCardControl_Title;
    }
}
