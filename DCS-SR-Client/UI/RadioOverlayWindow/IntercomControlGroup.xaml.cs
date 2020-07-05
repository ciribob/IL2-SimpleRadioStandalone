using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Overlay
{
    /// <summary>
    ///     Interaction logic for IntercomControlGroup.xaml
    /// </summary>
    public partial class IntercomControlGroup : UserControl
    {
        private bool _dragging;
        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly ConnectedClientsSingleton _connectClientsSingleton = ConnectedClientsSingleton.Instance;

        public IntercomControlGroup()
        {
            InitializeComponent();
        }
        public int RadioId { private get; set; }

        private void RadioSelectSwitch(object sender, RoutedEventArgs e)
        {
            var currentRadio = _clientStateSingleton.PlayerGameState.radios[RadioId];

            if (currentRadio.modulation != RadioInformation.Modulation.DISABLED)
            {
                if (_clientStateSingleton.PlayerGameState.control ==
                    PlayerGameState.RadioSwitchControls.HOTAS)
                {
                    _clientStateSingleton.PlayerGameState.selected = (short) RadioId;
                }
            }
        }

        private void RadioVolume_DragStarted(object sender, RoutedEventArgs e)
        {
            _dragging = true;
        }


        private void RadioVolume_DragCompleted(object sender, RoutedEventArgs e)
        {
            var currentRadio = _clientStateSingleton.PlayerGameState.radios[RadioId];

            if (currentRadio.modulation != RadioInformation.Modulation.DISABLED)
            {
                if (currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
                {
                    var clientRadio = _clientStateSingleton.PlayerGameState.radios[RadioId];

                    clientRadio.volume = (float) RadioVolume.Value / 100.0f;
                }
            }

            _dragging = false;
        }

        internal void RepaintRadioStatus()
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;

            if ((IL2PlayerRadioInfo == null) || !_clientStateSingleton.IsConnected)
            {
                RadioActive.Fill = new SolidColorBrush(Colors.Red);

                //reset dragging just incase
                _dragging = false;
            }
            else
            {
                var currentRadio = IL2PlayerRadioInfo.radios[RadioId];
                var transmitting = _clientStateSingleton.RadioSendingState;
                if (RadioId == IL2PlayerRadioInfo.selected || transmitting.IsSending && (transmitting.SendingOn == RadioId))
                {
                    if (transmitting.IsSending && (transmitting.SendingOn == RadioId))
                    {
                        RadioActive.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#96FF6D"));
                    }
                    else
                    {
                        RadioActive.Fill = new SolidColorBrush(Colors.Green);
                    }

                    var receiving = _clientStateSingleton.RadioReceivingState[0];

                    if (receiving!=null && receiving.IsReceiving)
                    {
                        RadioLabel.Foreground = new SolidColorBrush(Colors.White);
                        TunedCount.Foreground = RadioLabel.Foreground;
                    }
                    else
                    {
                        RadioLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E7E7E7"));
                        TunedCount.Foreground = RadioLabel.Foreground;
                    }

                    int count = _connectClientsSingleton.ClientsOnFreq(currentRadio.freq, currentRadio.modulation);

                    if (count > 0)
                    {
                        TunedCount.Content = "👤"+count;
                    }
                    else
                    {
                        TunedCount.Content = "";
                    }

                }
                else
                {
                    RadioActive.Fill = new SolidColorBrush(Colors.Orange);
                }

                if (_dragging == false)
                {
                    RadioVolume.Value = currentRadio.volume * 100.0;
                }
            }
        }
    }
}