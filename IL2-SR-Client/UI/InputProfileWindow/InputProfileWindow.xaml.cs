using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using MahApps.Metro.Controls;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.InputProfileWindow
{
    /// <summary>
    /// Interaction logic for InputProfileWindow.xaml
    /// </summary>
    public partial class InputProfileWindow : MetroWindow
    {
        public delegate void CreateProfileCallback(string profileName);

        private readonly CreateProfileCallback _callback;


        public InputProfileWindow(CreateProfileCallback callback, bool rename = false, string initialText = "")
        {
            InitializeComponent();
            _callback = callback;
            if (rename)
            {
                ProfileName.Text = initialText;
                CreateRename.Content = "Rename";
            }

        }

        private static string CleanString(string str)
        {
            string regexSearch = new string(System.IO.Path.GetInvalidFileNameChars()) + new string(System.IO.Path.GetInvalidPathChars());
            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            str = r.Replace(str, "").Replace(".cfg", "");

            if (str.Equals("default"))
            {
                return str + " 1";
            }

            return str.Trim();
        }
      
        private void CreateOrRename_Click(object sender, RoutedEventArgs e)
        {
            _callback(CleanString(ProfileName.Text));
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}