using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Newtonsoft.Json;
using NLog.Layouts;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Network
{
    public class NetworkMessage
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new JsonNetworkPropertiesResolver(),// strip out things not required for the TCP sync
            NullValueHandling = NullValueHandling.Ignore // same some network bandwidth
        };
        public enum MessageType
        {
            UPDATE, //META Data update - No Radio Information
            PING,
            SYNC,
            RADIO_UPDATE, //Only received server side
            SERVER_SETTINGS,
            CLIENT_DISCONNECT, // Client disconnected
            VERSION_MISMATCH,
            EXTERNAL_AWACS_MODE_PASSWORD, // Received server side to "authenticate"/pick side for external AWACS mode
            EXTERNAL_AWACS_MODE_DISCONNECT // Received server side on "voluntary" disconnect by the client (without closing the server connection)
        }

        public SRClient Client { get; set; }

        public MessageType MsgType { get; set; }

        public List<SRClient> Clients { get; set; }

        public Dictionary<string, string> ServerSettings { get; set; }

        public string ExternalAWACSModePassword { get; set; }

        public string Version { get; set; }

        public string Encode()
        {
            Version = UpdaterChecker.VERSION;
            return JsonConvert.SerializeObject(this, JsonSerializerSettings) + "\n";

        }
    }
}