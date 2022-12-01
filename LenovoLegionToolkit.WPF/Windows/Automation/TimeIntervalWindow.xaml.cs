using System;
using System.Windows;

namespace LenovoLegionToolkit.WPF.Windows.Automation;

public partial class TimeIntervalWindow
{
    public event EventHandler<(int, int)>? OnSave;
    
    public TimeIntervalWindow(int acInterval, int dcInterval)
    {
        InitializeComponent();
        _acTimeIntervalSeconds.Value = acInterval;
        _dcTimeIntervalSeconds.Value = dcInterval;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        
        int acInterval = (int)_acTimeIntervalSeconds.Value;
        int dcInterval = (int)_dcTimeIntervalSeconds.Value;

        OnSave?.Invoke(this, (acInterval, dcInterval));
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
