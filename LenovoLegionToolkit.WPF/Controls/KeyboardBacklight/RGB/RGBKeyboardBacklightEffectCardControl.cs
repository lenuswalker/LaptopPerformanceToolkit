using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

public class RGBKeyboardBacklightEffectCardControl : AbstractComboBoxRGBKeyboardCardControl<RGBKeyboardBacklightEffect>
{
    public RGBKeyboardBacklightEffectCardControl()
    {
        Icon = new SymbolIcon(SymbolRegular.Keyboard24);
        Title = Resource.RGBKeyboardBacklightEffectCardControl_Title;
    }
}
