using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Easy.MessageHub;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    public static class RadioHelper
    {
     
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

            if ((IL2PlayerRadioInfo != null)  &&
                radio < IL2PlayerRadioInfo.radios.Length && (radio >= 0))
            {
                return IL2PlayerRadioInfo.radios[radio];
            }

            return null;
        }

        public static void SelectRadioChannel(int channel, int radioId)
        {
            var currentRadio = GetRadio(radioId);

            if (currentRadio == null)
            {
                return;
            }
            var freq = PlayerGameState.START_FREQ +( PlayerGameState.CHANNEL_OFFSET * channel);

            currentRadio.freq = freq;

            currentRadio.channel = channel;

            MessageHub.Instance.Publish(new PlayerStateUpdate());
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
                    var chan = currentRadio.channel+1;

                    if (chan > PlayerGameState.CHANNEL_LIMIT)
                    {
                        chan = 1;
                    }

                    var freq = PlayerGameState.START_FREQ + (PlayerGameState.CHANNEL_OFFSET * chan);

                    currentRadio.freq = freq;

                    currentRadio.channel = chan;

                    MessageHub.Instance.Publish(new PlayerStateUpdate());
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
                    var chan = currentRadio.channel - 1;

                    if (chan < 1)
                    {
                        chan = PlayerGameState.CHANNEL_LIMIT;
                    }

                    var freq = PlayerGameState.START_FREQ + (PlayerGameState.CHANNEL_OFFSET * chan);

                    currentRadio.freq = freq;

                    currentRadio.channel = chan;

                    MessageHub.Instance.Publish(new PlayerStateUpdate());
                    
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