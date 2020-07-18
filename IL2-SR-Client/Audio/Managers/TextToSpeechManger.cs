using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Easy.MessageHub;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Managers
{
    public class TextToSpeechManger:IDisposable
    {
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly MixingSampleProvider mixer;
        private ISampleProvider sampleProvider;
        private Guid sub;
        private BufferedWaveProvider buffer;

        public TextToSpeechManger(MixingSampleProvider mixer)
        {
            this.mixer = mixer;
            sub = MessageHub.Instance.Subscribe<TextToSpeechMessage>(SpeakMessage);
            Init();
        }

        public void Init()
        { 
            buffer = new BufferedWaveProvider(new WaveFormat(mixer.WaveFormat.SampleRate, 16, mixer.WaveFormat.Channels));
            buffer.BufferDuration = TimeSpan.FromSeconds(10);
            buffer.DiscardOnBufferOverflow = true;
            sampleProvider = buffer.ToSampleProvider();
            mixer.AddMixerInput(sampleProvider);

            SpeakMessage(new TextToSpeechMessage(){Message = "IL2-SRS Text to Speech Activated"});
        }
        
        private void SpeakMessage(TextToSpeechMessage message)
        {
            var enabled = GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.EnableTextToSpeech);

            if (!enabled)
            {
                return;
            }
            
            Task task = new Task(() =>
            {
                try
                {
                    using (var synth = new SpeechSynthesizer())
                    using (var stream = new MemoryStream())
                    {
                        synth.SelectVoiceByHints(VoiceGender.Female,VoiceAge.Adult,0, new CultureInfo("en-GB", false));
                        synth.Rate = 1;
                        synth.Volume = 100;
                        if (mixer.WaveFormat.Channels == 2)
                        {
                            synth.SetOutputToAudioStream(stream,
                                new SpeechAudioFormatInfo(mixer.WaveFormat.SampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Stereo));
                        }
                        else
                        {
                            synth.SetOutputToAudioStream(stream,
                                new SpeechAudioFormatInfo(mixer.WaveFormat.SampleRate, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                        }
                        synth.Speak(message.Message);

                        //clear current message
                        buffer.ClearBuffer();
                        byte[] byteArr = stream.ToArray();
                        buffer.AddSamples(byteArr, 0, byteArr.Length);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,"Error with Text to Speech");
                }
            });
            task.Start();
        }

        public void Dispose()
        {
            MessageHub.Instance.UnSubscribe(sub);

            mixer?.RemoveMixerInput(sampleProvider);
        }
    }
}
