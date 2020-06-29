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

        public event PropertyChangedEventHandler PropertyChanged;

        public PlayerGameState PlayerGameState { get; }

        // Timestamp the last UDP Export broadcast was received from IL2, used for determining active game connection
        public long IL2ExportLastReceived { get; set; }

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

        public bool IsGameExportConnected { get { return IL2ExportLastReceived >= DateTime.Now.Ticks - 100000000; } }
        // Indicates an active game connection has been detected (1 tick = 100ns, 100000000 ticks = 10s stale timer),
        public bool IsGameConnected { get { return  IsGameExportConnected; } }

        public string LastSeenName { get; set; }

        private ClientStateSingleton()
        {
            RadioSendingState = new RadioSendingState();
            RadioReceivingState = new RadioReceivingState[11];

            ShortGUID = ShortGuid.NewGuid();
            PlayerGameState = new PlayerGameState();

            // The following members are not updated due to events. Therefore we need to setup a polling action so that they are
            // periodically checked.
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

    }
}