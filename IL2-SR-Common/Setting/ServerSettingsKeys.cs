using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Setting
{
    public enum ServerSettingsKeys
    {
        SERVER_PORT,
        COALITION_AUDIO_SECURITY,
        SPECTATORS_AUDIO_DISABLED,
        CLIENT_EXPORT_ENABLED,
        IRL_RADIO_TX,
        CLIENT_EXPORT_FILE_PATH,
        CHECK_FOR_BETA_UPDATES,
        SHOW_TUNED_COUNT,
        GLOBAL_LOBBY_FREQUENCIES,
        SHOW_TRANSMITTER_NAME,
        UPNP_ENABLED
    }

    public class DefaultServerSettings
    {
        public static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>()
        {
            { ServerSettingsKeys.CLIENT_EXPORT_ENABLED.ToString(), "false" },
            { ServerSettingsKeys.COALITION_AUDIO_SECURITY.ToString(), "false" },
            { ServerSettingsKeys.IRL_RADIO_TX.ToString(), "false" },
            { ServerSettingsKeys.SERVER_PORT.ToString(), "6002" },
            { ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED.ToString(), "false" },
            { ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH.ToString(), "clients-list.json" },
            { ServerSettingsKeys.CHECK_FOR_BETA_UPDATES.ToString(), "false" },
            { ServerSettingsKeys.SHOW_TUNED_COUNT.ToString(), "true" },
            { ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString(), "248.22" },
            { ServerSettingsKeys.UPNP_ENABLED.ToString(), "false" },
            { ServerSettingsKeys.SHOW_TRANSMITTER_NAME.ToString(), "true" },
        };
    }
}
