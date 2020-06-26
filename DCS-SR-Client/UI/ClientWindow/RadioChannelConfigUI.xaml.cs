using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for RadioChannelConfigUI.xaml
    /// </summary>
    public partial class RadioChannelConfigUi : UserControl
    {
        public RadioChannelConfigUi()
        {
            InitializeComponent();

            //I do this because at this point ProfileSettingKey hasn't been set
            //but it has when this is called
            ChannelSelector.Loaded += InitBalanceSlider;
        }

        public ProfileSettingsKeys ProfileSettingKey { get; set; }

        private void InitBalanceSlider(object sender, RoutedEventArgs e)
        {
            ChannelSelector.IsEnabled = false;
            Reload();

            ChannelSelector.ValueChanged += ChannelSelector_SelectionChanged;
        }

        public void Reload()
        {
            ChannelSelector.IsEnabled = false;
            var value = GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSetting(ProfileSettingKey).StringValue;

            float balance = 0f;
            if (value == null || value == "")
            {
                balance = 0f;
            }
            else
            {
                if (!float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out balance))
                {
                    if (value.ToUpper() == "LEFT")
                    {
                        balance = -1.0f;
                    }
                    else if (value.ToUpper() == "BOTH")
                    {
                        balance = 0f;
                    }
                    else if (value.ToUpper() == "RIGHT")
                    {
                        balance = 1.0f;
                    }
                }
            }

            GlobalSettingsStore.Instance.ProfileSettingsStore.SetClientSetting(ProfileSettingKey, balance.ToString(CultureInfo.InvariantCulture));

            ChannelSelector.Value = balance;

            ChannelSelector.IsEnabled = true;
        }

        private void ChannelSelector_SelectionChanged(object sender, EventArgs eventArgs)
        {
            //the selected value changes when 
            if (ChannelSelector.IsEnabled)
            {
                var selected = ChannelSelector.Value.ToString(CultureInfo.InvariantCulture);

                GlobalSettingsStore.Instance.ProfileSettingsStore.SetClientSetting(ProfileSettingKey, selected);
            }
        }
    }
}