using System;
using System.Diagnostics;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI;
using MathNet.Filtering;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.IL2.SimpleRadio.Standalone.Client.DSP;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using FragLabs.Audio.Codecs;
using NLog;
using static Ciribob.IL2.SimpleRadio.Standalone.Common.RadioInformation;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client
{
    public class ClientAudioProvider : AudioProvider
    {
        public static readonly int SILENCE_PAD = 200;

        private readonly Random _random = new Random();

        private int _lastReceivedOn = -1;
        private OnlineFilter[] _filters;

        private readonly BiQuadFilter _highPassFilter;
        private readonly BiQuadFilter _lowPassFilter;

        private OpusDecoder _decoder;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private short[] natoTone = null;
        private int natoPosition = 0;
        //used for comparison
        public static readonly short FM = Convert.ToInt16((int)RadioInformation.Modulation.FM);

        public ClientAudioProvider()
        {
            _filters = new OnlineFilter[2];
            _filters[0] =
                OnlineFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.INPUT_SAMPLE_RATE, 560, 3900);
            _filters[1] =
                OnlineFilter.CreateBandpass(ImpulseResponse.Finite, AudioManager.INPUT_SAMPLE_RATE, 100, 4500);

            JitterBufferProviderInterface =
                new JitterBufferProviderInterface(new WaveFormat(AudioManager.INPUT_SAMPLE_RATE, 2));

            SampleProvider = new Pcm16BitToSampleProvider(JitterBufferProviderInterface);

            _decoder = OpusDecoder.Create(AudioManager.INPUT_SAMPLE_RATE, 1);
            _decoder.ForwardErrorCorrection = false;

            _highPassFilter = BiQuadFilter.HighPassFilter(AudioManager.INPUT_SAMPLE_RATE, 520, 0.97f);
            _lowPassFilter = BiQuadFilter.LowPassFilter(AudioManager.INPUT_SAMPLE_RATE, 4130, 2.0f);

            var effect = new CachedAudioEffect(CachedAudioEffect.AudioEffectTypes.NATO_TONE);
            
            if (effect.AudioEffectBytes.Length > 0)
            {
                natoTone = ConversionHelpers.ByteArrayToShortArray(effect.AudioEffectBytes);

                var vol = Settings.GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.NATOToneVolume)
                    .FloatValue;

                for (int i = 0; i < natoTone.Length; i++)
                {
                    natoTone[i] = (short)(natoTone[i] * vol);
                }
            }
        }

        public JitterBufferProviderInterface JitterBufferProviderInterface { get; }
        public Pcm16BitToSampleProvider SampleProvider { get; }

        public long LastUpdate { get; private set; }

        //is it a new transmission?
        public bool LikelyNewTransmission()
        {
            //400 ms since last update
            long now = DateTime.Now.Ticks;
            if ((now - LastUpdate) > 4000000) //400 ms since last update
            {
                return true;
            }

            return false;
        }

        public void AddClientAudioSamples(ClientAudio audio)
        {
            //sort out volume
//            var timer = new Stopwatch();
//            timer.Start();

            bool newTransmission = LikelyNewTransmission();

            int decodedLength = 0;

            var decoded = _decoder.Decode(audio.EncodedAudio,
                audio.EncodedAudio.Length, out decodedLength, newTransmission);

            if (decodedLength <= 0)
            {
                Logger.Info("Failed to decode audio from Packet for client");
                return;
            }

            // for some reason if this is removed then it lags?!
            //guess it makes a giant buffer and only uses a little?
            //Answer: makes a buffer of 4000 bytes - so throw away most of it
            var tmp = new byte[decodedLength];
            Buffer.BlockCopy(decoded, 0, tmp, 0, decodedLength);

            audio.PcmAudioShort = ConversionHelpers.ByteArrayToShortArray(tmp);

            //adjust for LOS + Distance + Volume
            AdjustVolume(audio);

            if (globalSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffects))
            {
                if (audio.ReceivedRadio == 0)
                {
                    AddRadioEffectIntercom(audio);
                }
                else
                {
                    AddRadioEffect(audio);
                }
            }

            if (newTransmission)
            {
                // System.Diagnostics.Debug.WriteLine(audio.ClientGuid+"ADDED");
                //append ms of silence - this functions as our jitter buffer??
                var silencePad = (AudioManager.INPUT_SAMPLE_RATE / 1000) * SILENCE_PAD;

                var newAudio = new short[audio.PcmAudioShort.Length + silencePad];

                Buffer.BlockCopy(audio.PcmAudioShort, 0, newAudio, silencePad, audio.PcmAudioShort.Length);

                audio.PcmAudioShort = newAudio;
            }

            _lastReceivedOn = audio.ReceivedRadio;
            LastUpdate = DateTime.Now.Ticks;

            JitterBufferProviderInterface.AddSamples(new JitterBufferAudio
            {
                Audio =
                    SeperateAudio(ConversionHelpers.ShortArrayToByteArray(audio.PcmAudioShort),
                        audio.ReceivedRadio),
                PacketNumber = audio.PacketNumber
            });

            //timer.Stop();
        }

        private void AddRadioEffectIntercom(ClientAudio clientAudio)
        {
            var mixedAudio = clientAudio.PcmAudioShort;

            for (var i = 0; i < mixedAudio.Length; i++)
            {
                var audio = mixedAudio[i] / 32768f;

                audio = _highPassFilter.Transform(audio);

                if (float.IsNaN(audio))
                    audio = _lowPassFilter.Transform(mixedAudio[i]);
                else
                    audio = _lowPassFilter.Transform(audio);

                if (!float.IsNaN(audio))
                {
                    // clip
                    if (audio > 1.0f)
                        audio = 1.0f;
                    if (audio < -1.0f)
                        audio = -1.0f;

                    mixedAudio[i] = (short)(audio * 32767);
                }
            }

        }

        private void AdjustVolume(ClientAudio clientAudio)
        {

            var audio = clientAudio.PcmAudioShort;
            for (var i = 0; i < audio.Length; i++)
            {
                var speaker1Short = (short) (audio[i] * clientAudio.Volume);

                audio[i] = speaker1Short;
            }
        }


        private void AddRadioEffect(ClientAudio clientAudio)
        {
            var mixedAudio = clientAudio.PcmAudioShort;

            for (var i = 0; i < mixedAudio.Length; i++)
            {
                var audio = (double) mixedAudio[i] / 32768f;

                if (globalSettings.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping))
                {
                    if (audio > RadioFilter.CLIPPING_MAX)
                    {
                        audio = RadioFilter.CLIPPING_MAX;
                    }
                    else if (audio < RadioFilter.CLIPPING_MIN)
                    {
                        audio = RadioFilter.CLIPPING_MIN;
                    }
                }

                //high and low pass filter
                for (int j = 0; j < _filters.Length; j++)
                {
                    var filter = _filters[j];
                    audio = filter.ProcessSample(audio);

                    if (double.IsNaN(audio))
                        audio = (double) mixedAudio[j] / 32768f;
                    else
                    {
                        // clip
                        if (audio > 1.0f)
                            audio = 1.0f;
                        if (audio < -1.0f)
                            audio = -1.0f;
                    }
                }

                var shortAudio = (short)(audio * 32767);

                if (clientAudio.Modulation == FM
                    && natoTone !=null && globalSettings.GetClientSettingBool(ProfileSettingsKeys.NATOTone))
                {
                    shortAudio += natoTone[natoPosition];
                    natoPosition++;

                    if (natoPosition == natoTone.Length)
                    {
                        natoPosition = 0;
                    }
                }

                mixedAudio[i] = shortAudio;
            }
        }

        private void AddEncryptionFailureEffect(ClientAudio clientAudio)
        {
            var mixedAudio = clientAudio.PcmAudioShort;

            for (var i = 0; i < mixedAudio.Length; i++)
            {
                mixedAudio[i] = RandomShort();
            }
        }

        private short RandomShort()
        {
            //random short at max volume at eights
            return (short) _random.Next(-32768 / 8, 32768 / 8);
        }

        //destructor to clear up opus
        ~ClientAudioProvider()
        {
            _decoder.Dispose();
            _decoder = null;
        }

    }
}