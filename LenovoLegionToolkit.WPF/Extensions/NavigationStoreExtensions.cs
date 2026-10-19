using System.Linq;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Extensions;

public static class NavigationStoreExtensions
{
    public static void NavigateToNext(this NavigationView navigationView)
    {
        var navigationItems = navigationView.MenuItems.OfType<INavigationViewItem>().ToList();
        var current = navigationView.SelectedItem ?? navigationItems.FirstOrDefault();

        if (current is null)
            return;

        var index = (navigationItems.IndexOf(current) + 1) % navigationItems.Count;
        var next = navigationItems[index];

        navigationView.Navigate(next.TargetPageTag);
    }

    public static void NavigateToPrevious(this NavigationView navigationView)
    {
        var navigationItems = navigationView.MenuItems.OfType<INavigationViewItem>().ToList();
        var current = navigationView.SelectedItem ?? navigationItems.FirstOrDefault();

        if (current is null)
            return;

        var index = navigationItems.IndexOf(current) - 1;
        if (index < 0)
            index = navigationItems.Count - 1;
        var next = navigationItems[index];

        navigationView.Navigate(next.TargetPageTag);
    }
}
