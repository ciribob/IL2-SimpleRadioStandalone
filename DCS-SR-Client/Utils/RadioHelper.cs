using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    public static class RadioHelper
    {
     
       
        public static bool UpdateRadioFrequency(double frequency, int radioId, bool delta = true, bool inMHz = true)
        {
            bool inLimit = true;
            const double MHz = 1000000;

            if (inMHz)
            {
                frequency = frequency * MHz;
            }

            var radio = GetRadio(radioId);

            if (radio != null)
            {
                if (radio.modulation != RadioInformation.Modulation.DISABLED
                    && radio.modulation != RadioInformation.Modulation.INTERCOM
                    && radio.freqMode == RadioInformation.FreqMode.OVERLAY)
                {
                    if (delta)
                    {
                        radio.freq = (int)Math.Round(radio.freq + frequency);
                    }
                    else
                    {
                        radio.freq = (int)Math.Round(frequency);
                    }

                    //make sure we're not over or under a limit
                    if (radio.freq > radio.freqMax)
                    {
                        inLimit = false;
                        radio.freq = radio.freqMax;
                    }
                    else if (radio.freq < radio.freqMin)
                    {
                        inLimit = false;
                        radio.freq = radio.freqMin;
                    }

                    //set to no channel
                    radio.channel = -1;

                    //make radio data stale to force resysnc
                    ClientStateSingleton.Instance.LastSent = 0;
                }
            }
            return inLimit;
        }

        public static bool SelectRadio(int radioId)
        {
            var radio = GetRadio(radioId);

            if (radio != null)
            {
                if (radio.modulation != RadioInformation.Modulation.DISABLED
                    && ClientStateSingleton.Instance.PlayerGameState.control ==
                    PlayerGameState.RadioSwitchControls.HOTAS)
                {
                    ClientStateSingleton.Instance.PlayerGameState.selected = (short) radioId;
                    return true;
                }
            }

            return false;
        }

        public static RadioInformation GetRadio(int radio)
        {
            var IL2PlayerRadioInfo = ClientStateSingleton.Instance.PlayerGameState;

            if ((IL2PlayerRadioInfo != null) && IL2PlayerRadioInfo.IsCurrent() &&
                radio < IL2PlayerRadioInfo.radios.Length && (radio >= 0))
            {
                return IL2PlayerRadioInfo.radios[radio];
            }

            return null;
        }

    
        public static void SelectNextRadio()
        {
            var IL2PlayerRadioInfo = ClientStateSingleton.Instance.PlayerGameState;

            if ((IL2PlayerRadioInfo != null) && IL2PlayerRadioInfo.IsCurrent() &&
                IL2PlayerRadioInfo.control == PlayerGameState.RadioSwitchControls.HOTAS)
            {
                if (IL2PlayerRadioInfo.selected < 0
                    || IL2PlayerRadioInfo.selected > IL2PlayerRadioInfo.radios.Length
                    || IL2PlayerRadioInfo.selected + 1 > IL2PlayerRadioInfo.radios.Length)
                {
                    SelectRadio(1);

                    return;
                }
                else
                {
                    int currentRadio = IL2PlayerRadioInfo.selected;

                    //find next radio
                    for (int i = currentRadio + 1; i < IL2PlayerRadioInfo.radios.Length; i++)
                    {
                        if (SelectRadio(i))
                        {
                            return;
                        }
                    }

                    //search up to current radio
                    for (int i = 1; i < currentRadio; i++)
                    {
                        if (SelectRadio(i))
                        {
                            return;
                        }
                    }
                }
            }
        }

        public static void SelectPreviousRadio()
        {
            var IL2PlayerRadioInfo = ClientStateSingleton.Instance.PlayerGameState;

            if ((IL2PlayerRadioInfo != null) && IL2PlayerRadioInfo.IsCurrent() &&
                IL2PlayerRadioInfo.control == PlayerGameState.RadioSwitchControls.HOTAS)
            {
                if (IL2PlayerRadioInfo.selected < 0
                    || IL2PlayerRadioInfo.selected > IL2PlayerRadioInfo.radios.Length)
                {
                    IL2PlayerRadioInfo.selected = 1;
                    return;
                }
                else
                {
                    int currentRadio = IL2PlayerRadioInfo.selected;

                    //find previous radio
                    for (int i = currentRadio - 1; i > 0; i--)
                    {
                        if (SelectRadio(i))
                        {
                            return;
                        }
                    }

                    //search down to current radio
                    for (int i = IL2PlayerRadioInfo.radios.Length; i < currentRadio; i--)
                    {
                        if (SelectRadio(i))
                        {
                            return;
                        }
                    }
                }
            }
        }

       
        public static void SelectRadioChannel(PresetChannel selectedPresetChannel, int radioId)
        {
            if (UpdateRadioFrequency((double) selectedPresetChannel.Value, radioId, false, false))
            {
                var radio = GetRadio(radioId);

                if (radio != null) radio.channel = selectedPresetChannel.Channel;
            }
        }

        public static void RadioChannelUp(int radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null)
            {
                if (currentRadio.modulation != RadioInformation.Modulation.DISABLED
                    && ClientStateSingleton.Instance.PlayerGameState.control ==
                    PlayerGameState.RadioSwitchControls.HOTAS)
                {
                    //TODO
                }
            }
        }

        public static void RadioChannelDown(int radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null)
            {
                if (currentRadio.modulation != RadioInformation.Modulation.DISABLED
                    && ClientStateSingleton.Instance.PlayerGameState.control ==
                    PlayerGameState.RadioSwitchControls.HOTAS)
                {
                    //TODO
                }
            }
        }

        public static void SetRadioVolume(float volume, int radioId)
        {
            if (volume > 1.0)
            {
                volume = 1.0f;
            }else if (volume < 0)
            {
                volume = 0;
            }

            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null
                && currentRadio.modulation != RadioInformation.Modulation.DISABLED
                && currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
            {
                currentRadio.volume = volume;
            }
        }

    }
}