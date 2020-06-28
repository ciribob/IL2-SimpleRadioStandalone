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
        
        public double secFreq = 1;

        //should the radio restransmit?
        public bool retransmit = false;

        [JsonNetworkIgnoreSerialization]
        public float volume = 1.0f;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public FreqMode freqMode = FreqMode.COCKPIT;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public FreqMode guardFreqMode = FreqMode.COCKPIT;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public VolumeMode volMode = VolumeMode.COCKPIT;
        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public bool expansion = false;

        [JsonNetworkIgnoreSerialization]
        public int channel = -1;


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
            if (!PlayerRadioInfo.FreqCloseEnough(freq , compare.freq))
            {
                return false;
            }
            if (modulation != compare.modulation)
            {
                return false;
            }
            if (retransmit != compare.retransmit)
            {
                return false;
            }
            if (!PlayerRadioInfo.FreqCloseEnough(secFreq, compare.secFreq))
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
                guardFreqMode = this.guardFreqMode,
                modulation = this.modulation,
                secFreq = this.secFreq,
                name = this.name,
                volMode = this.volMode,
                volume = this.volume,
                retransmit = this.retransmit
            };
        }
    }
}