using System;
using System.Windows.Media.Imaging;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    public static class Images
    {
        public static BitmapImage IconConnected;
        public static BitmapImage IconDisconnected;
        public static BitmapImage IconDisconnectedError;
        public static BitmapImage IconDisconnectedGame;

        public static void Init()
        {
            // Image taken from https://icons8.com/icon/set/computer/metro @ 2018-08-01
            IconConnected = new BitmapImage(new Uri("pack://application:,,,/SR-ClientRadio;component/status-connected.png"));
            // Image taken from https://icons8.com/icon/set/computer/metro @ 2018-08-01
            IconDisconnected = new BitmapImage(new Uri("pack://application:,,,/SR-ClientRadio;component/status-disconnected.png"));
            // Image taken from https://icons8.com/icon/set/computer/metro @ 2018-08-01
            IconDisconnectedError = new BitmapImage(new Uri("pack://application:,,,/SR-ClientRadio;component/status-disconnected-error.png"));
            // Image taken from https://icons8.com/icon/set/computer/metro @ 2018-08-01
            IconDisconnectedGame = new BitmapImage(new Uri("pack://application:,,,/SR-ClientRadio;component/status-disconnected-game.png"));
        }
    }
}
