using MahApps.Metro.Controls;
using NLog;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientList
{
    /// <summary>
    /// Interaction logic for ClientListWindow.xaml
    /// </summary>
    public partial class ClientListWindow :  MetroWindow
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DispatcherTimer _updateTimer;

        private readonly ObservableCollection<ClientListModel> _clientList = new ObservableCollection<ClientListModel>();

        public ClientListWindow()
        {
            InitializeComponent();
            ClientList.ItemsSource = _clientList;
            UpdateList();

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void UpdateList()
        {
            _clientList.Clear();
            foreach (var srClient in ConnectedClientsSingleton.Instance.Values)
            {
                var client = new ClientListModel()
                {
                    Name = srClient.Name,
                    Coalition = srClient.Coalition
                };

                if (srClient.GameState.radios.Length >= 3)
                {
                    client.Channel = srClient.GameState.radios[1].Channel + "";

                    if (srClient.GameState.radios[2] != null &&
                        srClient.GameState.radios[2].modulation == RadioInformation.Modulation.AM)
                    {
                        client.Channel += ("-" + srClient.GameState.radios[2].channel);
                    }
                }
                else
                {
                    client.Channel = srClient.GameState.radios[1].Channel + "";
                }

                _clientList.Add(client);
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateList();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            _updateTimer?.Stop();
        }


    }
}
