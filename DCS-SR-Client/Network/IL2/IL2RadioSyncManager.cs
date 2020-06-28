using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Newtonsoft.Json;
using NLog;

/**
Keeps radio information in Sync Between IL2

**/

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2
{
    public class IL2RadioSyncManager
    {
        private readonly SendRadioUpdate _clientRadioUpdate;
        private readonly ClientSideUpdate _clientSideUpdate;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly UDPCommandHandler _udpCommandHandler; 
        private readonly IL2RadioSyncHandler il2RadioSyncHandler;

        public delegate void ClientSideUpdate();
        public delegate void SendRadioUpdate();

        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
        private DispatcherTimer _clearRadio;

        public bool IsListening { get; private set; }

        public IL2RadioSyncManager(SendRadioUpdate clientRadioUpdate, ClientSideUpdate clientSideUpdate,
           string guid, IL2RadioSyncHandler.NewAircraft _newAircraftCallback)
        {
            _clientRadioUpdate = clientRadioUpdate;
            _clientSideUpdate = clientSideUpdate;
            IsListening = false;
            _udpCommandHandler = new UDPCommandHandler();
            il2RadioSyncHandler = new IL2RadioSyncHandler(clientRadioUpdate, _newAircraftCallback);

            _clearRadio = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) { Interval = TimeSpan.FromSeconds(1) };
            _clearRadio.Tick += CheckIfRadioIsStale;
        }

        private void CheckIfRadioIsStale(object sender, EventArgs e)
        {
            if (!_clientStateSingleton.PlayerRadioInfo.IsCurrent())
            {
                //check if we've had an update
                if (_clientStateSingleton.PlayerRadioInfo.LastUpdate > 0)
                {
                    _clientStateSingleton.PlayerRadioInfo.Reset();

                    _clientRadioUpdate();
                    _clientSideUpdate();
                    Logger.Info("Reset Radio state - no longer connected");
                }
            }
        }

        public void Start()
        {
            IL2Listener();
            IsListening = true;
        }


        private void IL2Listener()
        {
            il2RadioSyncHandler.Start();
            _udpCommandHandler.Start();
             _clearRadio.Start();
        }

        public void Stop()
        {
            IsListening = false;

            _clearRadio.Stop();
            il2RadioSyncHandler.Stop();
            _udpCommandHandler.Stop();
            
        }
    }
}