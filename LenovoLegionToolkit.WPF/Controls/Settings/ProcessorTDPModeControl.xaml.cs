using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.WPF.Controls.Settings
{
    public partial class ProcessorTDPModeControl
    {
        private readonly Dictionary<TDPMode, TDPLimits> _setting;
        private readonly ProcessorController _controller = IoCContainer.Resolve<ProcessorController>();

        public ProcessorTDPModeControl(Dictionary<TDPMode, TDPLimits> setting)
        {
            _setting = setting;

            InitializeComponent();

            _headerControl.Title = _setting.Keys.First().ToString();

            _textBoxStapmLimit.Text = _setting[_setting.Keys.First()].Stapm.ToString();
            _textBoxFastLimit.Text = _setting[_setting.Keys.First()].Fast.ToString();
            _textBoxSlowLimit.Text = _setting[_setting.Keys.First()].Slow.ToString();
        }

        private async void TextBoxStapmLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(_textBoxStapmLimit.Text, out int stapmLimit))
            {
                TDPMode tdpMode = _setting.Keys.First();
                TDPLimits limits = new()
                {
                    Stapm = stapmLimit,
                    Fast = _setting[_setting.Keys.First()].Fast,
                    Slow = _setting[_setting.Keys.First()].Slow
                };
                await _controller.SetSettingsAsync(tdpMode, limits);
            }
        }

        private async void TextBoxFastLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(_textBoxFastLimit.Text, out int fastLimit))
            {
                TDPMode tdpMode = _setting.Keys.First();
                TDPLimits limits = new()
                {
                    Stapm = _setting[_setting.Keys.First()].Stapm,
                    Fast = fastLimit,
                    Slow = _setting[_setting.Keys.First()].Slow
                };
                await _controller.SetSettingsAsync(tdpMode, limits);
            }
        }

        private async void TextBoxSlowLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(_textBoxSlowLimit.Text, out int slowLimit))
            {
                TDPMode tdpMode = _setting.Keys.First();
                TDPLimits limits = new()
                {
                    Stapm = _setting[_setting.Keys.First()].Stapm,
                    Fast = _setting[_setting.Keys.First()].Fast,
                    Slow = slowLimit
                };
                await _controller.SetSettingsAsync(tdpMode, limits);
            }          
        }
    }
}
