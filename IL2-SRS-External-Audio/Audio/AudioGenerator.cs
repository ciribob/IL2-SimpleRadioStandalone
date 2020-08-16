using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Managers;
using FragLabs.Audio.Codecs;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.ExternalAudio.Audio
{
    public class AudioGenerator
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly string path;

        public static readonly int INPUT_SAMPLE_RATE = 16000;
        public static readonly int INPUT_AUDIO_LENGTH_MS = 40;

        public static readonly int SEGMENT_FRAMES = (INPUT_SAMPLE_RATE / 1000) * INPUT_AUDIO_LENGTH_MS
            ; //640 is 40ms as INPUT_SAMPLE_RATE / 1000 *40 = 640

        private readonly float volume;

        public AudioGenerator(string path, float volume)
        {
            this.path = path;
            this.volume = volume;
        }

        private byte[] TextToSpeech()
        {
            try
            {
                using (var synth = new SpeechSynthesizer())
                using (var stream = new MemoryStream())
                {
                    synth.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult, 0, new CultureInfo("en-GB", false));
                    synth.Rate = 1;

                    var intVol = (int)(volume * 100.0);

                    if (intVol > 100)
                    {
                        intVol = 100;
                    }

                    synth.Volume = intVol;
                    
                    synth.SetOutputToAudioStream(stream,
                        new SpeechAudioFormatInfo(INPUT_SAMPLE_RATE, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                    
                    synth.Speak(path);

                    return stream.ToArray();
                   
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error with Text to Speech");
            }
             return new byte[0];
        }

        private IWaveProvider GetMP3WaveProvider()
        {
            Logger.Info($"Reading MP3 @ {path}");

            var mp3Reader = new Mp3FileReader(path);
            int bytes = (int)mp3Reader.Length;
            byte[] buffer = new byte[bytes];

            Logger.Info($"Read MP3 @ {mp3Reader.WaveFormat}");

            int read = mp3Reader.Read(buffer, 0, (int)bytes);
            BufferedWaveProvider bufferedWaveProvider = new BufferedWaveProvider(mp3Reader.WaveFormat)
            {
                BufferLength = read * 2, ReadFully = false, DiscardOnBufferOverflow = true
            };

            bufferedWaveProvider.AddSamples(buffer, 0, read);
            VolumeSampleProvider volumeSample =
                new VolumeSampleProvider(bufferedWaveProvider.ToSampleProvider()) {Volume = volume};

            mp3Reader.Close();
            mp3Reader.Dispose();

            Logger.Info($"Convert to Mono 16bit PCM");

            //after this we've got 16 bit PCM Mono  - just need to sort sample rate
            return volumeSample.ToMono().ToWaveProvider16(); 
        }

        private byte[] GetMP3Bytes()
        {
            List<byte> resampledBytesList = new List<byte>();
            var waveProvider = GetMP3WaveProvider();

            Logger.Info($"Convert to Mono 16bit PCM 16000KHz from {waveProvider.WaveFormat}");
            //loop thorough in up to 1 second chunks
            var resample = new EventDrivenResampler(waveProvider.WaveFormat, new WaveFormat(INPUT_SAMPLE_RATE, 1));

            byte[] buffer = new byte[waveProvider.WaveFormat.AverageBytesPerSecond * 2];

            int read = 0;
            while ((read = waveProvider.Read(buffer, 0, waveProvider.WaveFormat.AverageBytesPerSecond)) > 0)
            {
                //resample as we go
                resampledBytesList.AddRange(resample.ResampleBytes(buffer, read));
            }

            Logger.Info($"Converted to Mono 16bit PCM 16000KHz from {waveProvider.WaveFormat}");

            return resampledBytesList.ToArray();
        }

        public List<byte[]> GetOpusBytes()
        {
            List<byte[]> opusBytes = new List<byte[]>();

            byte[] resampledBytes;

            if (path.ToLower().EndsWith(".mp3"))
            {
                Logger.Info($"Reading MP3 it looks like a file");
                resampledBytes = GetMP3Bytes();
            }
            else
            {
                Logger.Info($"Doing Text To Speech as its not an MP3 path");
                resampledBytes = TextToSpeech();
            }

            Logger.Info($"Encode as Opus");
            var _encoder = OpusEncoder.Create(INPUT_SAMPLE_RATE, 1, FragLabs.Audio.Codecs.Opus.Application.Voip);

            int pos = 0;
            while (pos +(SEGMENT_FRAMES*2) < resampledBytes.Length)
            {
                
                byte[] buf = new byte[SEGMENT_FRAMES * 2];
                Buffer.BlockCopy(resampledBytes, pos,buf,0,SEGMENT_FRAMES*2);

                var outLength = 0;
                var frame = _encoder.Encode(buf, buf.Length, out outLength);

                if (outLength > 0)
                {
                    //create copy with small buffer
                    var encoded = new byte[outLength];

                    Buffer.BlockCopy(frame, 0, encoded, 0, outLength);

                    opusBytes.Add(encoded);
                }

                pos += (SEGMENT_FRAMES * 2);
            }
            
            if (pos+1 < resampledBytes.Length)
            {
                //last bit - less than 40 ms
                byte[] buf = new byte[SEGMENT_FRAMES * 2];
                Buffer.BlockCopy(resampledBytes, pos, buf, 0, resampledBytes.Length - pos);

                var outLength = 0;
                var frame = _encoder.Encode(buf, buf.Length, out outLength);

                if (outLength > 0)
                {
                    //create copy with small buffer
                    var encoded = new byte[outLength];

                    Buffer.BlockCopy(frame, 0, encoded, 0, outLength);

                    opusBytes.Add(encoded);
                }

            }
            
            _encoder.Dispose();
            Logger.Info($"Finished encoding as Opus");

            return opusBytes;
        }
    }
}
