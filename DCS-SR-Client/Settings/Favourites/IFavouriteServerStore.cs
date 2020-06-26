using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Preferences
{
    public interface IFavouriteServerStore
    {
        IEnumerable<ServerAddress> LoadFromStore();

        bool SaveToStore(IEnumerable<ServerAddress> addresses);
    }
}