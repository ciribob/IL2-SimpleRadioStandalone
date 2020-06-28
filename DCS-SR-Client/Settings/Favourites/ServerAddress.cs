using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    public class ServerAddress : INotifyPropertyChanged
    {
        public ServerAddress(string name, string address, bool isDefault)
        {
            // Set private values directly so we don't trigger useless re-saving of favourites list when being loaded for the first time
            _name = name;
            _address = address;
            IsDefault = isDefault; // Explicitly use property setter here since IsDefault change includes additional logic
        }

        private string _name;
        public string Name {
            get
            {
                return _name;
            }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _address;
        public string Address
        {
            get
            {
                return _address;
            }
            set
            {
                if (_address != value)
                {
                    _address = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _eamCoalitionPassword;
        public string EAMCoalitionPassword
        {
            get
            {
                return _eamCoalitionPassword;
            }
            set
            {
                if (_eamCoalitionPassword != value)
                {
                    _eamCoalitionPassword = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isDefault;
        public bool IsDefault
        {
            get { return _isDefault; }
            set
            {
                _isDefault = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}