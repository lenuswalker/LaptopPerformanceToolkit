using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Settings;

namespace LenovoLegionToolkit.WPF.Controls.Settings
{
    public partial class ProcessorTDPModeControl
    {
        //private readonly ProcessorSettings _settings;

        private readonly Dictionary<TDPMode, TDPLimits> _settings;

        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();

        public ProcessorTDPModeControl(Dictionary<TDPMode, TDPLimits> settings)
        {
            _settings = settings;

            InitializeComponent();
            _headerControl.Title = _settings.Keys.First().ToString();

            _textBoxStapmLimit.Text = _settings[_settings.Keys.First()].Stapm.ToString();
            _textBoxFastLimit.Text = _settings[_settings.Keys.First()].Fast.ToString();
            _textBoxSlowLimit.Text = _settings[_settings.Keys.First()].Slow.ToString();

            //var selectedItem = setting.CPUBoostModes.First(cbm => cbm.Value == setting.ACSettingValue);
            //_comboBoxAC.SetItems(setting.CPUBoostModes, selectedItem, s => s.Name);

            //selectedItem = setting.CPUBoostModes.First(cbm => cbm.Value == setting.DCSettingValue);
            //_comboBoxDC.SetItems(setting.CPUBoostModes, selectedItem, s => s.Name);
        }

        //private async void ComboBoxAC_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (!_comboBoxAC.TryGetSelectedItem(out CPUBoostMode cpuBoostMode))
        //        return;

        //    await _controller.SetSettingAsync(setting.PowerPlan, cpuBoostMode, true);
        //}

        //private async void ComboBoxDC_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //{
        //    if (!_comboBoxDC.TryGetSelectedItem(out CPUBoostMode cpuBoostMode))
        //        return;

        //    await _controller.SetSettingAsync(setting.PowerPlan, cpuBoostMode, false);
        //}

        private async void TextBoxStapmLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            TDPMode tdpMode = _settings.Keys.First();
            TDPLimits limits = GetLimits();
            await _controller.SetSettingsAsync(tdpMode, limits);
        }

        private async void TextBoxFastLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            TDPMode tdpMode = _settings.Keys.First();
            TDPLimits limits = GetLimits();
            await _controller.SetSettingsAsync(tdpMode, limits);
        }

        private async void TextBoxSlowLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            TDPMode tdpMode = _settings.Keys.First();
            TDPLimits limits = GetLimits();
            await _controller.SetSettingsAsync(tdpMode, limits);
        }

        public TDPLimits GetLimits()
        {
            TDPLimits limits = new();
            if (int.TryParse(_textBoxStapmLimit.Text, out int stapmLimit))
                if (int.TryParse(_textBoxFastLimit.Text, out int fastLimit))
                    if (int.TryParse(_textBoxSlowLimit.Text, out int slowLimit))
                    {
                        limits.Stapm = stapmLimit;
                        limits.Fast = fastLimit;
                        limits.Slow = slowLimit;
                    }
            return limits;
        }
    }
}
