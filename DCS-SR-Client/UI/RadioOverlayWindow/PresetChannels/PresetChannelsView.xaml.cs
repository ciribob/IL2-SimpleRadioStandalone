using System.Windows.Controls;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow.PresetChannels
{
    /// <summary>
    /// Interaction logic for PresetChannelsView.xaml
    /// </summary>
    public partial class PresetChannelsView : UserControl
    {
        public PresetChannelsView()
        {
            InitializeComponent();

            //set to window width
            FrequencyDropDown.Width = Width;
        }
    }
}