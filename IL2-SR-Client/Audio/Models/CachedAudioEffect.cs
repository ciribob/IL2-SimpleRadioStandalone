using System;
using System.IO;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Properties;
using NAudio.Wave;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client
{
    public class CachedAudioEffect
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public enum AudioEffectTypes
        {
            RADIO_TX = 0,
            RADIO_RX = 1,
        }

        //order must match ENUM above
        private static readonly string[] FileNameLookup = new[] { "Radio-TX-1600.wav","Radio-RX-1600.wav"};

        public CachedAudioEffect(AudioEffectTypes audioEffect)
        {
            AudioEffectType = audioEffect;

            var file = GetFile();

            AudioEffectBytes = new byte[0];

            if (file != null)
            {
                using (var reader = new WaveFileReader(file))
                {
                    //    Assert.AreEqual(16, reader.WaveFormat.BitsPerSample, "Only works with 16 bit audio");
                    if (reader.WaveFormat.BitsPerSample == 16 && reader.WaveFormat.Channels == 1)
                    {
                        AudioEffectBytes = new byte[reader.Length];
                        var read = reader.Read(AudioEffectBytes, 0, AudioEffectBytes.Length);
                        Logger.Info($"Read Effect {audioEffect} from {file} Successfully");
                    }
                    else
                    {
                        Logger.Info($"Unable to read Effect {audioEffect} from {file} Successfully - not 16 bits!");
                    }

                }
            }
            else
            {
                Logger.Info($"Unable to find file for effect {audioEffect} in AudioEffects\\{FileNameLookup[(int) audioEffect]} ");
            }

        }

        public AudioEffectTypes AudioEffectType { get; }

        public byte[] AudioEffectBytes { get; }

        private string GetFile()
        {
            var location = AppDomain.CurrentDomain.BaseDirectory+"\\AudioEffects\\";

            if(File.Exists(location + FileNameLookup[(int)AudioEffectType]))
            {
                return location + FileNameLookup[(int) AudioEffectType];
            }

            return null;
        }
    }
}