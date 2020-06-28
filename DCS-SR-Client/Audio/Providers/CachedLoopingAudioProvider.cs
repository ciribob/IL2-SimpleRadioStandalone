using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using NAudio.Wave;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Providers
{
    public class CachedLoopingAudioProvider:IWaveProvider
    {
        private int _position = 0;

        private readonly short[] _audioEffectShort;
        private IWaveProvider source;

        public CachedLoopingAudioProvider(IWaveProvider source, WaveFormat waveFormat, CachedAudioEffect.AudioEffectTypes effectType)
        {
            this.WaveFormat = waveFormat;
            var effect = new CachedAudioEffect(effectType);
            _audioEffectShort = ConversionHelpers.ByteArrayToShortArray(effect.AudioEffectBytes);

            this.source = source;
        }

        public WaveFormat WaveFormat { get; }
        public int Read(byte[] buffer, int offset, int count)
        {
            int read = source.Read(buffer, offset, count);

            if (!GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.NATOTone))
            {
                return read;
            }

            var effectBytes = GetEffect(read / 2);

            //mix together
            for (int i = 0; i < read / 2; i++)
            {
                short audio = ConversionHelpers.ToShort(buffer[(offset + i) * 2], buffer[((i + offset) * 2) + 1]);

                audio = (short)(audio + _audioEffectShort[i]);

                //buffer[i + offset] = effectBytes[i]+buffer[i + offset];

                byte byte1;
                byte byte2;
                ConversionHelpers.FromShort(audio, out byte1, out byte2);

                buffer[(offset + i) * 2] = byte1;
                buffer[((i + offset) * 2) + 1] = byte2;
            }

            return read;
        }

        private short[] GetEffect(int count)
        {
            short[] loopedEffect = new short[count];

            var i = 0;
            while (i < count)
            {
                loopedEffect[i] = _audioEffectShort[_position];
                _position++;

                if (_position == _audioEffectShort.Length)
                {
                    _position = 0;
                }

                i++;
            }

            return loopedEffect;
        }
    }
}
