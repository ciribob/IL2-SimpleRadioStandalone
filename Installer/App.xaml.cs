using System.Globalization;
using System.Threading;
using System.Windows;

namespace Installer
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public string[] Arguments = new string[0];
        private void ApplicationStartup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                Arguments = e.Args;
            }
           
        }
    }
}