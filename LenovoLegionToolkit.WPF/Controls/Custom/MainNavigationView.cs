using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LenovoLegionToolkit.WPF.Controls.Custom;

public class MainNavigationView : Wpf.Ui.Controls.NavigationView
{
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_NavigationViewContentPresenter") is not DependencyObject current)
            return;

        while (!ReferenceEquals(current, this))
        {
            if (current is Border border)
            {
                border.SetCurrentValue(Border.CornerRadiusProperty, new CornerRadius(8));
                border.SetCurrentValue(Border.BorderThicknessProperty, new Thickness(1));
                return;
            }

            if (current is not Visual && current is not System.Windows.Media.Media3D.Visual3D)
                return;

            var parent = VisualTreeHelper.GetParent(current);
            if (parent is null)
                return;

            current = parent;
        }
    }
}
