using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
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
                    var current = ClientStateSingleton.Instance.PlayerGameState.selected;

                    ClientStateSingleton.Instance.PlayerGameState.selected = (short) radioId;

                    //only send audio if we switched
                    if (current != ClientStateSingleton.Instance.PlayerGameState.selected)
                    {
                        if (radioId == 0)
                        {
                            MessageHub.Instance.Publish(new TextToSpeechMessage()
                            {
                                Message = "Selected Intercom"
                            });
                        }
                        else
                        {
                            MessageHub.Instance.Publish(new TextToSpeechMessage()
                            {
                                Message = "Selected Radio "+radioId
                            });
                        }
                    }
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

            MessageHub.Instance.Publish(new TextToSpeechMessage()
            {
                Message = "Channel " + channel
            });

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
                    var wrap = GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.WrapNextRadio);
                    
                    var chan = currentRadio.channel+1;

                    var limit = SyncedServerSettings.Instance.GetSettingInt(ServerSettingsKeys.CHANNEL_LIMIT);
                    if (chan > limit)
                    {
                        if (wrap)
                        {
                            chan = 1;
                        }
                        else
                        {
                            chan = limit;
                        }
                    }

                    var freq = PlayerGameState.START_FREQ + (PlayerGameState.CHANNEL_OFFSET * chan);

                    currentRadio.freq = freq;

                    currentRadio.channel = chan;

                    MessageHub.Instance.Publish(new PlayerStateUpdate());
                    MessageHub.Instance.Publish(new TextToSpeechMessage()
                    {
                        Message = "Channel "+chan
                    });
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
                    var wrap = GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.WrapNextRadio);

                    var chan = currentRadio.channel - 1;

                    var limit = SyncedServerSettings.Instance.GetSettingInt(ServerSettingsKeys.CHANNEL_LIMIT);
                    
                    if (chan < 1)
                    {
                        if (wrap)
                        {
                            chan = limit;
                        }
                        else
                        {
                            chan = 1;
                        }
                    }

                    var freq = PlayerGameState.START_FREQ + (PlayerGameState.CHANNEL_OFFSET * chan);

                    currentRadio.freq = freq;

                    currentRadio.channel = chan;

                    MessageHub.Instance.Publish(new TextToSpeechMessage()
                    {
                        Message = "Channel " + chan
                    });

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

        public static void PreviousRadio()
        {
            var selected = ClientStateSingleton.Instance.PlayerGameState.selected;

            if (selected - 1 < 0)
            {
                //radio 2 if its not disabled - else one
                for (int i = ClientStateSingleton.Instance.PlayerGameState.radios.Length - 1; i >= 0; i--)
                {
                    if (SelectRadio(i))
                    {
                        return;
                    }

                }
            }
            else
            {
                for (int i = selected-1; i >= 0; i--)
                {
                    if (SelectRadio(i))
                    {
                        return;
                    }

                }
            }
            //looped
            SelectRadio(0);
        }

        public static void NextRadio()
        {
            var selected = ClientStateSingleton.Instance.PlayerGameState.selected;

            if (selected + 1 > ClientStateSingleton.Instance.PlayerGameState.radios.Length)
            {
                SelectRadio(0);
            }
            else
            {
                //find next radios
                for (int i = selected + 1; i < ClientStateSingleton.Instance.PlayerGameState.radios.Length; i++)
                {
                    if (SelectRadio(i))
                    {
                        return;
                    }
                }
            }
            //looped
            SelectRadio(0);
        }
    }
}