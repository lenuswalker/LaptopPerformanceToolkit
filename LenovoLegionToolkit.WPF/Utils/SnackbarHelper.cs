using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.WPF.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Utils;

public static class SnackbarHelper
{
    private static readonly ConditionalWeakTable<SnackbarPresenter, SnackbarService> Services = new();

    public static Task ShowAsync(string title, string? message = null, SnackbarType type = SnackbarType.Success)
    {
        Show(title, message, type);
        return Task.CompletedTask;
    }

    public static void Show(string title, string? message = null, SnackbarType type = SnackbarType.Success)
    {
        if (Application.Current.MainWindow is not MainWindow mainWindow)
            return;

        var presenter = mainWindow.SnackbarPresenter;
        var service = Services.GetValue(presenter, static snackbarPresenter =>
        {
            var snackbarService = new SnackbarService();
            snackbarService.SetSnackbarPresenter(snackbarPresenter);
            return snackbarService;
        });

        service.Show(title,
            message ?? string.Empty,
            GetAppearance(type),
            new SymbolIcon(GetIcon(type)),
            TimeSpan.FromMilliseconds(GetTimeout(title, message, type)));

        if (presenter.Content is Snackbar snackbar)
            snackbar.IsCloseButtonEnabled = type != SnackbarType.Success;
    }

    private static ControlAppearance GetAppearance(SnackbarType type) => type switch
    {
        SnackbarType.Warning => ControlAppearance.Caution,
        SnackbarType.Error => ControlAppearance.Danger,
        _ => ControlAppearance.Secondary
    };

    private static SymbolRegular GetIcon(SnackbarType type) => type switch
    {
        SnackbarType.Warning => SymbolRegular.Warning24,
        SnackbarType.Error => SymbolRegular.ErrorCircle24,
        SnackbarType.Info => SymbolRegular.Info24,
        _ => SymbolRegular.Checkmark24
    };

    private static int GetTimeout(string title, string? message, SnackbarType type) => type switch
    {
        SnackbarType.Success => 2000,
        _ => Math.Clamp(GetTextLengthInMilliseconds(title, message), 5000, 10000)
    };

    private static int GetTextLengthInMilliseconds(string title, string? message)
    {
        var length = 2 + (title.Length + (message?.Length ?? 0)) % 10;
        return length * 1000;
    }
}
