using System;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Audio
{
    public abstract class AudioProvider
    {
        protected readonly Settings.ProfileSettingsStore globalSettings;

        public AudioProvider()
        {
            globalSettings = Settings.GlobalSettingsStore.Instance.ProfileSettingsStore;
        }

        public byte[] SeperateAudio(byte[] pcmAudio, int radioId)
        {
            var settingType = ProfileSettingsKeys.Radio1Channel;

            if (radioId == 0)
            {
                settingType = ProfileSettingsKeys.IntercomChannel;
            }
            else if (radioId == 1)
            {
                settingType = ProfileSettingsKeys.Radio1Channel;
            }
            else if (radioId == 2)
            {
                settingType = ProfileSettingsKeys.Radio2Channel;
            }
            else if (radioId == 3)
            {
                settingType = ProfileSettingsKeys.Radio3Channel;
            }
            else if (radioId == 4)
            {
                settingType = ProfileSettingsKeys.Radio4Channel;
            }
            else if (radioId == 5)
            {
                settingType = ProfileSettingsKeys.Radio5Channel;
            }
            else if (radioId == 6)
            {
                settingType = ProfileSettingsKeys.Radio6Channel;
            }
            else if (radioId == 7)
            {
                settingType = ProfileSettingsKeys.Radio7Channel;
            }
            else if (radioId == 8)
            {
                settingType = ProfileSettingsKeys.Radio8Channel;
            }
            else if (radioId == 9)
            {
                settingType = ProfileSettingsKeys.Radio9Channel;
            }
            else if (radioId == 10)
            {
                settingType = ProfileSettingsKeys.Radio10Channel;
            }
            else
            {
                return CreateBalancedMix(pcmAudio,0);
            }

            float balance = 0;
            try
            {
                balance  = globalSettings.GetClientSetting(settingType).FloatValue;
            }
            catch (Exception ex)
            {
                //ignore
            }

            return CreateBalancedMix(pcmAudio, balance);
        }

        public static byte[] CreateBalancedMix(byte[] pcmAudio, float balance)
        {
            float left = 1.0f;
            float right = 1.0f;

            //right
            if (balance > 0)
            {
                var leftBias = 1- Math.Abs(balance);
                var rightBias = Math.Abs(balance);
                //right
                left = left * leftBias;
                right = right * rightBias;
            }
            else if (balance < 0)
            {
                var leftBias = Math.Abs(balance);
                var rightBias = 1 - Math.Abs(balance);
                //left
                left = left * leftBias;
                right = right * rightBias;
            }
            else
            {
                //equal balance
                left = 0.5f;
                right = 0.5f;
            }

            if(left > 1f)
            {
                left = 1f;
            }
            if (right > 1f)
            {
                right = 1f;
            }

            var stereoMix = new byte[pcmAudio.Length * 2];
            for (var i = 0; i < pcmAudio.Length / 2; i++)
            {
                float audio = ConversionHelpers.ToShort(pcmAudio[i * 2], pcmAudio[i * 2 + 1]);

                short leftAudio = 0;
                short rightAudio = 0;
                //half audio to keep loudness the same
                if (audio != 0)
                {
                    leftAudio = Convert.ToInt16(audio * left);
                    rightAudio = Convert.ToInt16(audio * right);
                }

                byte byte1;
                byte byte2;
                ConversionHelpers.FromShort(leftAudio, out byte1, out byte2);

                byte byte3;
                byte byte4;
                ConversionHelpers.FromShort(rightAudio, out byte3, out byte4);

                stereoMix[i * 4] = byte1;
                stereoMix[i * 4 + 1] = byte2;

                stereoMix[i * 4 + 2] = byte3;
                stereoMix[i * 4 + 3] = byte4;
            }
            return stereoMix;
        }
    }
}