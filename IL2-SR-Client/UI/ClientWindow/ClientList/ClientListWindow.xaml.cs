using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using MahApps.Metro.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;

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
                _clientList.Add(new ClientListModel()
                {
                    Channel =  srClient.GameState.radios[1].Channel,
                    Name = srClient.Name,
                    Coalition = srClient.Coalition
                });
            }

            ClientList.ItemsSource = _clientList;
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
