using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models
{
    public struct CombinedRadioState
    {
        public DCSPlayerRadioInfo RadioInfo;

        public RadioSendingState RadioSendingState;

        public RadioReceivingState[] RadioReceivingState;

        public int ClientCountConnected;

        public int[] TunedClients;
    }
}