using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Newtonsoft.Json;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientList
{
    public class ClientListModel:INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;

        public int Coalition
        {
            get { return _coalition; }
            set
            {
                _coalition = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Coalition"));
            }
        }

        private int _coalition;

        public string ClientGuid { get; set; }

        public int Channel
        {
            get => channel;
            set
            {
                if (channel != value)
                {
                    channel = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Channel"));
                }
            }
        }

        private string _name = "";
        private int channel;

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (value == null || value == "")
                {
                    value = "---";
                }

                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
                }
            }
        }
        public SolidColorBrush ClientCoalitionColour
        {
            get
            {
                switch (Coalition)
                {
                    case 0:
                        return new SolidColorBrush(Colors.White);
                    case 1:
                        return new SolidColorBrush(Colors.Red);
                    case 2:
                        return new SolidColorBrush(Colors.Blue);
                    default:
                        return new SolidColorBrush(Colors.White);
                }
            }
        }
    }
}
