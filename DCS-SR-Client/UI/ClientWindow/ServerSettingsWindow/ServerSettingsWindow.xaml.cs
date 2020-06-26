using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using MahApps.Metro.Controls;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for ServerSettingsWindow.xaml
    /// </summary>
    public partial class ServerSettingsWindow : MetroWindow
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DispatcherTimer _updateTimer;

        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        public ServerSettingsWindow()
        {
            InitializeComponent();

            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(1)};
            _updateTimer.Tick += UpdateUI;
            _updateTimer.Start();

            UpdateUI(null, null);
        }

        private void UpdateUI(object sender, EventArgs e)
        {
            var settings = _serverSettings;

            try
            {
                SpectatorAudio.Content = settings.GetSettingAsBool(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED)
                    ? "DISABLED"
                    : "ENABLED";

                CoalitionSecurity.Content = settings.GetSettingAsBool(ServerSettingsKeys.COALITION_AUDIO_SECURITY)
                    ? "ON"
                    : "OFF";

                LineOfSight.Content = settings.GetSettingAsBool(ServerSettingsKeys.LOS_ENABLED) ? "ON" : "OFF";

                Distance.Content = settings.GetSettingAsBool(ServerSettingsKeys.DISTANCE_ENABLED) ? "ON" : "OFF";

                RealRadio.Content = settings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX) ? "ON" : "OFF";

                RadioRXInterference.Content =
                    settings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_RX_INTERFERENCE) ? "ON" : "OFF";

                RadioExpansion.Content = settings.GetSettingAsBool(ServerSettingsKeys.RADIO_EXPANSION) ? "ON" : "OFF";

                ExternalAWACSMode.Content = settings.GetSettingAsBool(ServerSettingsKeys.EXTERNAL_AWACS_MODE) ? "ON" : "OFF";

                AllowRadioEncryption.Content = settings.GetSettingAsBool(ServerSettingsKeys.ALLOW_RADIO_ENCRYPTION) ? "ON" : "OFF";

                TunedClientCount.Content = settings.GetSettingAsBool(ServerSettingsKeys.SHOW_TUNED_COUNT) ? "ON" : "OFF";

                ShowTransmitterName.Content = settings.GetSettingAsBool(ServerSettingsKeys.SHOW_TRANSMITTER_NAME) ? "ON" : "OFF";

                ServerVersion.Content = SRSClientSyncHandler.ServerVersion;

                NodeLimit.Content = settings.RetransmitNodeLimit;
            }
            catch (IndexOutOfRangeException ex)
            {
                Logger.Warn("Missing Server Option - Connected to old server");
            }
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            _updateTimer.Stop();
        }
    }
}