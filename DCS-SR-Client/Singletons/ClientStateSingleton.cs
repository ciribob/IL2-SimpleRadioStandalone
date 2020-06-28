using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons
{
    public sealed class ClientStateSingleton : INotifyPropertyChanged
    {
        private static volatile ClientStateSingleton _instance;
        private static object _lock = new Object();

        public delegate bool RadioUpdatedCallback();

        private List<RadioUpdatedCallback> _radioCallbacks = new List<RadioUpdatedCallback>();

        public event PropertyChangedEventHandler PropertyChanged;

        public PlayerRadioInfo PlayerRadioInfo { get; }

        // Timestamp the last UDP Game GUI broadcast was received from IL2, used for determining active game connection
        public long IL2GameGuiLastReceived { get; set; }

        // Timestamp the last UDP Export broadcast was received from IL2, used for determining active game connection
        public long IL2ExportLastReceived { get; set; }

        // Timestamp for the last time 
        public long LotATCLastReceived { get; set; }

        public long LastSent { get; set; }

        private static readonly DispatcherTimer _timer = new DispatcherTimer();

        public RadioSendingState RadioSendingState { get; set; }
        public  RadioReceivingState[] RadioReceivingState { get; }

        private bool isConnected;
        public bool IsConnected
        {
            get
            {
                return isConnected;
            }
            set
            {
                isConnected = value;
                NotifyPropertyChanged("IsConnected");
            }
        }

        private bool isVoipConnected;
        public bool IsVoipConnected
        {
            get
            {
                return isVoipConnected;
            }
            set
            {
                isVoipConnected = value;
                NotifyPropertyChanged("IsVoipConnected");
            }
        }

        private bool isConnectionErrored;
        public string ShortGUID { get; }

        public bool IsConnectionErrored
        {
            get
            {
                return isConnectionErrored;
            }
            set
            {
                isConnectionErrored = value;
                NotifyPropertyChanged("isConnectionErrored");
            }
        }



        public bool IsLotATCConnected { get { return LotATCLastReceived >= DateTime.Now.Ticks - 50000000; } }

        public bool IsGameGuiConnected { get { return IL2GameGuiLastReceived >= DateTime.Now.Ticks - 100000000; } }
        public bool IsGameExportConnected { get { return IL2ExportLastReceived >= DateTime.Now.Ticks - 100000000; } }
        // Indicates an active game connection has been detected (1 tick = 100ns, 100000000 ticks = 10s stale timer), not updated by EAM
        public bool IsGameConnected { get { return IsGameGuiConnected && IsGameExportConnected; } }

        public string LastSeenName { get; set; }

        private ClientStateSingleton()
        {
            RadioSendingState = new RadioSendingState();
            RadioReceivingState = new RadioReceivingState[11];

            ShortGUID = ShortGuid.NewGuid();
            PlayerRadioInfo = new PlayerRadioInfo();

            // The following members are not updated due to events. Therefore we need to setup a polling action so that they are
            // periodically checked.
            IL2GameGuiLastReceived = 0;
            IL2ExportLastReceived = 0;
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => {
                NotifyPropertyChanged("IsGameConnected");
                NotifyPropertyChanged("IsLotATCConnected");
                NotifyPropertyChanged("ExternalAWACSModeConnected");
            };
            _timer.Start();


            LastSent = 0;

            IsConnected = false;

            LastSeenName = Settings.GlobalSettingsStore.Instance.GetClientSetting(Settings.GlobalSettingsKeys.LastSeenName).StringValue;
        }

        public static ClientStateSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new ClientStateSingleton();
                    }
                }

                return _instance;
            }
        }

        private void NotifyPropertyChanged(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool ShouldUseLotATCPosition()
        {
            if (!IsLotATCConnected)
            {
                return false;
            }

            if (IsGameExportConnected)
            {
                if (PlayerRadioInfo.inAircraft)
                {
                    return false;
                }
            }

            return true;
        }

     

    }
}