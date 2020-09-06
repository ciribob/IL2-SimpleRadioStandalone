using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Overlay;
using Easy.MessageHub;
using Newtonsoft.Json;
using NLog;

/**
Keeps radio information in Sync Between IL2

**/

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2
{
    public class IL2RadioSyncManager
    {
    
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly UDPCommandHandler _udpCommandHandler; 
        private readonly IL2RadioSyncHandler il2RadioSyncHandler;

        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
        private DispatcherTimer _clearRadio;

        public bool IsListening { get; private set; }

        public IL2RadioSyncManager()
        {
            IsListening = false;
            _udpCommandHandler = new UDPCommandHandler();
            il2RadioSyncHandler = new IL2RadioSyncHandler();

            _clearRadio = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) { Interval = TimeSpan.FromSeconds(5) };
            _clearRadio.Tick += CheckIfRadioIsStale;
            Start();
        }

        private void CheckIfRadioIsStale(object sender, EventArgs e)
        {
            //kept current by any UDP traffic from IL2
            if (!_clientStateSingleton.PlayerGameState.IsCurrent())
            {
                _clientStateSingleton.PlayerGameState.LastUpdate = -1;
                Logger.Info("Reset Radio state - IL2 not running");
                _clientStateSingleton.PlayerGameState.coalition = 0;
                _clientStateSingleton.PlayerGameState.unitId = 0;
                _clientStateSingleton.PlayerGameState.vehicleId = -1;

                MessageHub.Instance.Publish(new PlayerStateUpdate());
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