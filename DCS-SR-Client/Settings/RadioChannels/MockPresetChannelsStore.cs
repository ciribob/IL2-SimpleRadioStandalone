using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Preferences;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings.RadioChannels
{
    public class MockPresetChannelsStore : IPresetChannelsStore
    {
        public IEnumerable<PresetChannel> LoadFromStore(string radioName)
        {
            IList<PresetChannel> _presetChannels = new List<PresetChannel>();

            _presetChannels.Add(new PresetChannel
            {
                Text = 127.1 + "",
                Value = 127.1
            });

            _presetChannels.Add(new PresetChannel
            {
                Text = 127.1 + "",
                Value = 127.1
            });

            _presetChannels.Add(new PresetChannel
            {
                Text = 127.1 + "",
                Value = 127.1
            });

            return _presetChannels;
        }
    }
}