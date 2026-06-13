using System;
using System.Linq;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Macro;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Pages;

public partial class MacroPage
{
    private readonly MacroController _controller = IoCContainer.Resolve<MacroController>();

    public MacroPage()
    {
        Initialized += MacroPage_Initialized;

        InitializeComponent();
    }

    private void MacroPage_Initialized(object? sender, EventArgs e)
    {
        _enableMacroToggle.IsChecked = _controller.IsEnabled;

        var zeroNumberButton = _numberPad.Children.OfType<Button>().Last();
        Reload(zeroNumberButton);
    }

    private void EnableMacroToggle_Click(object sender, RoutedEventArgs e)
    {
        var enabled = _enableMacroToggle.IsChecked ?? false;
        _controller.SetEnabled(enabled);

        // Install/remove the low-level keyboard hook live. This runs on the UI thread, which owns the
        // message loop the hook callback is dispatched on, so the hook only exists while macros are on.
        if (enabled)
            _controller.Start();
        else
            _controller.Stop();
    }

    private void NumberPadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        _numberPad.Children
            .OfType<Button>()
            .ForEach(b => b.Appearance = ControlAppearance.Secondary);

        Reload(button);
    }

    private void Reload(Button button)
    {
        button.Appearance = ControlAppearance.Primary;

        var key = Convert.ToUInt64((string)button.Tag, 16);
        _sequenceControl.Set(new(MacroSource.Keyboard, key));
    }
}
