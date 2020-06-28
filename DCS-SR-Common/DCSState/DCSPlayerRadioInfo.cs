using System;
using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Newtonsoft.Json;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common
{
    public class DCSPlayerRadioInfo
    {
        //HOTAS or IN COCKPIT controls
        public enum RadioSwitchControls
        {
            HOTAS = 0,
            IN_COCKPIT = 1
        }

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public string name = "";

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public bool inAircraft = false;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public volatile bool ptt = false;

        public RadioInformation[] radios = new RadioInformation[2]; //1 + intercom

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public RadioSwitchControls control = RadioSwitchControls.HOTAS;

        [JsonNetworkIgnoreSerialization]
        public short selected = 0;

        public string unit = "";
        
        public uint unitId;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public bool intercomHotMic = false; //if true switch to intercom and transmit

        [JsonIgnore]
        public readonly static uint UnitIdOffset = 100000001
            ; // this is where non aircraft "Unit" Ids start from for satcom intercom

        [JsonNetworkIgnoreSerialization]
        public SimultaneousTransmissionControl simultaneousTransmissionControl =
            SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL;

        public enum SimultaneousTransmissionControl
        {
            ENABLED_INTERNAL_SRS_CONTROLS = 1,
            EXTERNAL_DCS_CONTROL = 0,
        }

        public DCSPlayerRadioInfo()
        {
            for (var i = 0; i < radios.Length; i++)
            {
                radios[i] = new RadioInformation();
            }
        }

        [JsonIgnore]
        public long LastUpdate { get; set; }

        public void Reset()
        {
            name = "";
            ptt = false;
            selected = 0;
            unit = "";
            simultaneousTransmissionControl = SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL;
            LastUpdate = 0;

            for (var i = 0; i < radios.Length; i++)
            {
                radios[i] = new RadioInformation();
            }

        }

        // override object.Equals
        public override bool Equals(object compare)
        {
            try
            {
                if ((compare == null) || (GetType() != compare.GetType()))
                {
                    return false;
                }

                var compareRadio = compare as DCSPlayerRadioInfo;

                if (control != compareRadio.control)
                {
                    return false;
                }
                //if (side != compareRadio.side)
                //{
                //    return false;
                //}
                if (!name.Equals(compareRadio.name))
                {
                    return false;
                }
                if (!unit.Equals(compareRadio.unit))
                {
                    return false;
                }

                if (unitId != compareRadio.unitId)
                {
                    return false;
                }

                if (inAircraft != compareRadio.inAircraft)
                {
                    return false;
                }

                for (var i = 0; i < radios.Length; i++)
                {
                    var radio1 = radios[i];
                    var radio2 = compareRadio.radios[i];

                    if ((radio1 != null) && (radio2 != null))
                    {
                        if (!radio1.Equals(radio2))
                        {
                            return false;
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
          

            return true;
        }


        /*
         * Was Radio updated in the last 10 Seconds
         */

        public bool IsCurrent()
        {
            return LastUpdate > DateTime.Now.Ticks - 100000000;
        }

        //comparing doubles is risky - check that we're close enough to hear (within 100hz)
        public static bool FreqCloseEnough(double freq1, double freq2)
        {
            var diff = Math.Abs(freq1 - freq2);

            return diff < 500;
        }

        public RadioInformation CanHearTransmission(double frequency,
            RadioInformation.Modulation modulation,
            byte encryptionKey,
            uint sendingUnitId,
            List<int> blockedRadios,
            out RadioReceivingState receivingState)
        {
        //    if (!IsCurrent())
       //     {
       //         receivingState = null;
        //        decryptable = false;
         //       return null;
         //   }

            RadioInformation bestMatchingRadio = null;
            RadioReceivingState bestMatchingRadioState = null;

            for (var i = 0; i < radios.Length; i++)
            {
                var receivingRadio = radios[i];

                if (receivingRadio != null)
                {
                    //handle INTERCOM Modulation is 2
                    if ((receivingRadio.modulation == RadioInformation.Modulation.INTERCOM) &&
                        (modulation == RadioInformation.Modulation.INTERCOM))
                    {
                        if ((unitId > 0) && (sendingUnitId > 0)
                            && (unitId == sendingUnitId) )
                        {
                            receivingState = new RadioReceivingState
                            {
                                IsSecondary = false,
                                LastReceivedAt = DateTime.Now.Ticks,
                                ReceivedOn = i
                            };
                            return receivingRadio;
                        }
                        receivingState = null;
                        return null;
                    }

                    if (modulation == RadioInformation.Modulation.DISABLED
                        || receivingRadio.modulation == RadioInformation.Modulation.DISABLED)
                    {
                        continue;
                    }

                    //within 1khz
                    if ((FreqCloseEnough(receivingRadio.freq,frequency))
                        && (receivingRadio.modulation == modulation)
                        && (receivingRadio.freq > 10000))
                    {
                        if ( !blockedRadios.Contains(i))
                        {
                            receivingState = new RadioReceivingState
                            {
                                IsSecondary = false,
                                LastReceivedAt = DateTime.Now.Ticks,
                                ReceivedOn = i
                            };
                            return receivingRadio;
                        }

                        bestMatchingRadio = receivingRadio;
                        bestMatchingRadioState = new RadioReceivingState
                        {
                            IsSecondary = false,
                            LastReceivedAt = DateTime.Now.Ticks,
                            ReceivedOn = i
                        };
                    }
                    if ((receivingRadio.secFreq == frequency)
                        && (receivingRadio.secFreq > 10000))
                    {
                       
                        receivingState = new RadioReceivingState
                        {
                            IsSecondary = true,
                            LastReceivedAt = DateTime.Now.Ticks,
                            ReceivedOn = i
                        };
                        return receivingRadio;
                        
                    }
                }
            }
            receivingState = bestMatchingRadioState;
            return bestMatchingRadio;
        }

        public DCSPlayerRadioInfo DeepClone()
        {
            var clone = (DCSPlayerRadioInfo) this.MemberwiseClone();

            //ignore position
            clone.radios = new RadioInformation[11];

            for (var i = 0; i < 11; i++)
            {
                clone.radios[i] = this.radios[i].Copy();
            }

            return clone;

        }
    }
}