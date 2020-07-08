using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings.RadioChannels
{
    public interface IPresetChannelsStore
    {
        IEnumerable<PresetChannel> LoadFromStore(string radioName);
    }
}