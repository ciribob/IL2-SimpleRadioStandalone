using System;
using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Newtonsoft.Json;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common
{
    public class PlayerGameState
    {
        //HOTAS or IN COCKPIT controls
        public enum RadioSwitchControls
        {
            HOTAS = 0,
            IN_COCKPIT = 1
        }

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public volatile bool ptt = false;

        public RadioInformation[] radios = new RadioInformation[3]; //2 + intercom

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public RadioSwitchControls control = RadioSwitchControls.HOTAS;

        [JsonNetworkIgnoreSerialization]
        public short selected = 1;

        public short coalition = 0;
        
        public int unitId = 0;

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public bool intercomHotMic = false; //if true switch to intercom and transmit


        [JsonNetworkIgnoreSerialization]
        public SimultaneousTransmissionControl simultaneousTransmissionControl =
            SimultaneousTransmissionControl.EXTERNAL_IL2_CONTROL;


        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public static readonly double CHANNEL_OFFSET = 1000000; //for channel <> Freq conversion

        [JsonNetworkIgnoreSerialization]
        [JsonIL2IgnoreSerialization]
        public static readonly double START_FREQ = 250*1000000; //for channel <> Freq conversion

        public int vehicleId = -1;

        public enum SimultaneousTransmissionControl
        {
            ENABLED_INTERNAL_SRS_CONTROLS = 1,
            EXTERNAL_IL2_CONTROL = 0,
        }

        public PlayerGameState()
        {
            radios[0] = new RadioInformation()
                {
                    channel = -1,
                    expansion = false,
                    freq = 10000,
                    freqMode = RadioInformation.FreqMode.OVERLAY,
                    freqMax = 10000,
                    freqMin = 10000,
                    modulation = RadioInformation.Modulation.INTERCOM,
                    volMode = RadioInformation.VolumeMode.OVERLAY,
                    volume = 1.0f,
                    name = "INTERCOM",
                };

            radios[1] = new RadioInformation()
            {
                channel = 1,
                expansion = false,
                freq = START_FREQ+CHANNEL_OFFSET,//add offset as channel 1
                freqMode = RadioInformation.FreqMode.OVERLAY,
                freqMax = 3e+8,
                freqMin = 2e+8,
                modulation = RadioInformation.Modulation.AM,
                volMode = RadioInformation.VolumeMode.OVERLAY,
                volume = 1.0f,
                name = "RADIO 1",
            };
            radios[2] = new RadioInformation()
            {
                channel = 1,
                expansion = false,
                freq = START_FREQ + CHANNEL_OFFSET,//add offset as channel 1
                freqMode = RadioInformation.FreqMode.OVERLAY,
                freqMax = 3e+8,
                freqMin = 2e+8,
                modulation = RadioInformation.Modulation.DISABLED,
                volMode = RadioInformation.VolumeMode.OVERLAY,
                volume = 1.0f,
                name = "RADIO 2",
            };

        }

        [JsonIgnore]
        public long LastUpdate { get; set; }

        // override object.Equals
        public override bool Equals(object compare)
        {
            try
            {
                if ((compare == null) || (GetType() != compare.GetType()))
                {
                    return false;
                }

                var compareRadio = compare as PlayerGameState;

                if (control != compareRadio.control)
                {
                    return false;
                }
                if (coalition != compareRadio.coalition)
                {
                    return false;
                }

                if (unitId != compareRadio.unitId)
                {
                    return false;
                }

                if (vehicleId != compareRadio.vehicleId)
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
         * Was State updated in the last 10 Seconds
         */

        public bool IsCurrent()
        {
            return new TimeSpan(DateTime.Now.Ticks - LastUpdate).TotalSeconds < 10;
        }

        //comparing doubles is risky - check that we're close enough to hear (within 100hz)
        public static bool FreqCloseEnough(double freq1, double freq2)
        {
            var diff = Math.Abs(freq1 - freq2);

            return diff < 500;
        }
        public RadioInformation CanHearTransmission(double frequency,
            RadioInformation.Modulation modulation,
            long sendingUnitId,
            long sendingVehicleId,
            List<int> blockedRadios,
            out RadioReceivingState receivingState)
        {

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
                        if (((unitId == vehicleId && unitId > -1) 
                             || (vehicleId == sendingVehicleId && vehicleId > -1)))
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
                   
                }
            }
            receivingState = bestMatchingRadioState;
            return bestMatchingRadio;
        }

        public PlayerGameState DeepClone()
        {
            var clone = (PlayerGameState) this.MemberwiseClone();

            //ignore position
            clone.radios = new RadioInformation[this.radios.Length];

            for (var i = 0; i < clone.radios.Length; i++)
            {
                clone.radios[i] = this.radios[i].Copy();
            }

            return clone;

        }
    }
}