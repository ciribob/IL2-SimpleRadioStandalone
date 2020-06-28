using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow
{
    /// <summary>
    ///     Interaction logic for RadioControlGroup.xaml
    /// </summary>
    public partial class RadioControlGroup : UserControl
    {
        private const double MHz = 1000000;
        private bool _dragging;
        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly ConnectedClientsSingleton _connectClientsSingleton = ConnectedClientsSingleton.Instance;

        public RadioControlGroup()
        {
            this.DataContext = this; // set data context

            InitializeComponent();
        }

        private int _radioId;

        public int RadioId
        { 
            get { return _radioId; }
            set
            {
                _radioId = value;
            }
        }

        private void RadioSelectSwitch(object sender, RoutedEventArgs e)
        {
            RadioHelper.SelectRadio(RadioId);
        }

        private void RadioFrequencyText_Click(object sender, MouseButtonEventArgs e)
        {
            RadioHelper.SelectRadio(RadioId);
        }

        private void RadioVolume_DragStarted(object sender, RoutedEventArgs e)
        {
            _dragging = true;
        }


        private void RadioVolume_DragCompleted(object sender, RoutedEventArgs e)
        {
            var currentRadio = _clientStateSingleton.PlayerRadioInfo.radios[RadioId];

            if (currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
            {
                var clientRadio = _clientStateSingleton.PlayerRadioInfo.radios[RadioId];

                clientRadio.volume = (float) RadioVolume.Value / 100.0f;
            }

            _dragging = false;
        }

        private void ToggleButtons(bool enable)
        {
            if (enable)
            {
                Channel1.Visibility = Visibility.Visible;
                Channel2.Visibility = Visibility.Visible;
                Channel3.Visibility = Visibility.Visible;
                Channel4.Visibility = Visibility.Visible;
                Channel5.Visibility = Visibility.Visible;

                Channel1.IsEnabled = true;
                Channel2.IsEnabled = true;
                Channel3.IsEnabled = true;
                Channel4.IsEnabled = true;
                Channel5.IsEnabled = true;

            }
            else
            {
                Channel1.Visibility = Visibility.Hidden;
                Channel2.Visibility = Visibility.Hidden;
                Channel3.Visibility = Visibility.Hidden;
                Channel4.Visibility = Visibility.Hidden;
                Channel5.Visibility = Visibility.Hidden;

                Channel1.IsEnabled = true;
                Channel2.IsEnabled = true;
                Channel3.IsEnabled = true;
                Channel4.IsEnabled = true;
                Channel5.IsEnabled = true;

            }
        }

        internal void RepaintRadioStatus()
        {

            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerRadioInfo;

            if ((IL2PlayerRadioInfo == null) || !IL2PlayerRadioInfo.IsCurrent())
            {
                RadioActive.Fill = new SolidColorBrush(Colors.Red);
                RadioFrequency.Text = "Unknown";

                RadioVolume.IsEnabled = false;

                TunedClients.Visibility = Visibility.Hidden;

                ToggleButtons(false);

                //reset dragging just incase
                _dragging = false;
            }
            else
            {
                var currentRadio = IL2PlayerRadioInfo.radios[RadioId];

                if (currentRadio == null)
                {
                    return;
                }

                var transmitting = _clientStateSingleton.RadioSendingState;
                if (RadioId == IL2PlayerRadioInfo.selected)
                {

                    if (transmitting.IsSending && (transmitting.SendingOn == RadioId))
                    {
                        RadioActive.Fill = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#96FF6D"));
                    }
                    else
                    {
                        RadioActive.Fill = new SolidColorBrush(Colors.Green);
                    }
                }
                else
                {
                   
                    RadioActive.Fill = new SolidColorBrush(Colors.Orange);
                    
                    
                }

                if (currentRadio.modulation == RadioInformation.Modulation.DISABLED) // disabled
                {
                    RadioActive.Fill = new SolidColorBrush(Colors.Red);
                    RadioFrequency.Text = "Unknown";

                    RadioVolume.IsEnabled = false;

                    TunedClients.Visibility = Visibility.Hidden;

                    ToggleButtons(false);
                    return;
                }

               

                if (currentRadio.modulation == RadioInformation.Modulation.INTERCOM) //intercom
                {
                    RadioFrequency.Text = "INTERCOM";
                }
                else
                {
                    RadioFrequency.Text =
                        (currentRadio.freq / MHz).ToString("0.000",
                            CultureInfo.InvariantCulture); //make nuber UK / US style with decimals not commas!
                        
                    if(currentRadio.modulation == RadioInformation.Modulation.AM)
                    {
                        RadioFrequency.Text += "AM";
                    }
                    else if(currentRadio.modulation == RadioInformation.Modulation.FM)
                    {
                        RadioFrequency.Text += "FM";
                    }
                    else
                    {
                        RadioFrequency.Text += "";
                    }

                    if (currentRadio.secFreq > 100)
                    {
                        RadioFrequency.Text += " G";
                    }

                    if (currentRadio.channel >= 0)
                    {
                        RadioFrequency.Text += " C" + currentRadio.channel;
                    }

                }


                int count = _connectClientsSingleton.ClientsOnFreq(currentRadio.freq, currentRadio.modulation);

                if (count > 0)
                {
                    TunedClients.Text = "👤" + count;
                    TunedClients.Visibility = Visibility.Visible;
                }
                else
                {
                    TunedClients.Visibility = Visibility.Hidden;
                }


                if (currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
                {
                    RadioVolume.IsEnabled = true;

                    //reset dragging just incase
                    //    _dragging = false;
                }
                else
                {
                    RadioVolume.IsEnabled = false;

                    //reset dragging just incase
                    //  _dragging = false;
                }

                ToggleButtons(currentRadio.freqMode == RadioInformation.FreqMode.OVERLAY);

                if (_dragging == false)
                {
                    RadioVolume.Value = currentRadio.volume * 100.0;
                }
               
            }

        }

        internal void RepaintRadioReceive()
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerRadioInfo;
            if (IL2PlayerRadioInfo == null)
            {
                RadioFrequency.Foreground = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
            }
            else
            {
                var receiveState = _clientStateSingleton.RadioReceivingState[RadioId];
                //check if current

                if ((receiveState == null) || !receiveState.IsReceiving)
                {
                    RadioFrequency.Foreground =
                        new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
                }
                else if ((receiveState != null) && receiveState.IsReceiving)
                {
                    if (receiveState.SentBy.Length > 0)
                    {
                        RadioFrequency.Text = receiveState.SentBy;
                    }

                    if (receiveState.IsSecondary)
                    {
                        RadioFrequency.Foreground = new SolidColorBrush(Colors.Red);
                    }
                    else
                    {
                        RadioFrequency.Foreground = new SolidColorBrush(Colors.White);
                    }
                }
                else
                {
                    RadioFrequency.Foreground =
                        new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
                }
            }
        }


        private void ChannelOne_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void ChannelTwo_Click(object sender, RoutedEventArgs e)
        {
            

        }

        private void ChannelThree_Click(object sender, RoutedEventArgs e)
        {
            

        }

        private void ChannelFour_Click(object sender, RoutedEventArgs e)
        {
            

        }

        private void ChannelFive_Click(object sender, RoutedEventArgs e)
        {
            
        }
    }
}