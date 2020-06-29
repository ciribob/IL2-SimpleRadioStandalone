using System;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Newtonsoft.Json;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common
{
    public class RadioInformation
    {
        public enum VolumeMode
        {
            COCKPIT = 0,
            OVERLAY = 1,
        }

        public enum FreqMode
        {
            COCKPIT = 0,
            OVERLAY = 1,
        }


        public enum Modulation
        {
            AM = 0,
            FM = 1,
            INTERCOM = 2,
            DISABLED = 3
        }

        [JsonIL2IgnoreSerialization]
        [JsonNetworkIgnoreSerialization]
        public double freqMax = 1;

        [JsonIL2IgnoreSerialization]
        [JsonNetworkIgnoreSerialization]
        public double freqMin = 1;

        public double freq = 1;
        
        public Modulation modulation = Modulation.DISABLED;

        [JsonNetworkIgnoreSerialization]
        public string name = "";

        [JsonNetworkIgnoreSerialization]
        public float volume = 1.0f;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public FreqMode freqMode = FreqMode.COCKPIT;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public VolumeMode volMode = VolumeMode.COCKPIT;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public bool expansion = false;

        [JsonNetworkIgnoreSerialization]
        public int channel = -1;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public static readonly double CHANNEL_OFFSET = 1000000; //for channel <> Freq conversion

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public static readonly int CHANNEL_LIMIT = 5;
        
        /**
         * Used to determine if we should send an update to the server or not
         * We only need to do that if something that would stop us Receiving happens which
         * is frequencies and modulation
         */

        public override bool Equals(object obj)
        {
            if ((obj == null) || (GetType() != obj.GetType()))
                return false;

            var compare = (RadioInformation) obj;

            if (!name.Equals(compare.name))
            {
                return false;
            }
            if (!PlayerGameState.FreqCloseEnough(freq , compare.freq))
            {
                return false;
            }
            if (modulation != compare.modulation)
            {
                return false;
            }
          

            return true;
        }

        internal RadioInformation Copy()
        {
            //probably can use memberswise clone
            return new RadioInformation()
            {
                channel = this.channel,
                expansion = this.expansion,
                freq = this.freq,
                freqMax = this.freqMax,
                freqMin = this.freqMin,
                freqMode = this.freqMode,
                modulation = this.modulation,
                name = this.name,
                volMode = this.volMode,
                volume = this.volume,
            };
        }
    }
}