using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using NLog;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client
{
    public class DejitterBuffer
    {
        //    private List<List<ClientAudio>> clientAudioBuffer = new List<List<ClientAudio>>(5);
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();


        private readonly long _bufferLength = 100; //in ms

        private readonly Dictionary<string, List<byte>> _clientBuffers = new Dictionary<string, List<byte>>();
        private long _firstPacketTime;

        public DejitterBuffer()
        {
            for (var i = 0; i < 5; i++)
            {
                //      clientAudioBuffer.Add(new List<ClientAudio>());
            }

            _firstPacketTime = long.MaxValue; //stops audio buffer playing
        }

        [DllImport("kernel32.dll")]
        private static extern long GetTickCount64();


        public void AddAudio(ClientAudio audio)
        {
            if (_firstPacketTime > GetTickCount64())
            {
                //      logger.Info("Start");
                _firstPacketTime = audio.ReceiveTime;
                _clientBuffers.Clear();
                _clientBuffers[audio.ClientGuid] = new List<byte>(1920*5); //asumes 5 sets worth of 20ms PCM audio
                _clientBuffers[audio.ClientGuid].AddRange(audio.PcmAudio);
            }
            else
            {
                //work out which buffer
                var diff = audio.ReceiveTime - _firstPacketTime;

                if (diff < 0 || diff > _bufferLength)
                {
                    //drop too early or to late
                    //TODO tune the too early? an old packet would knacker the queue
                    Logger.Warn("Dropping Packet - Diff: " + diff);
                }
                else
                {
                    if (!_clientBuffers.ContainsKey(audio.ClientGuid))
                    {
                        _clientBuffers[audio.ClientGuid] = new List<byte>();
                        _clientBuffers[audio.ClientGuid].AddRange(audio.PcmAudio);
                    }
                    else
                    {
                        //   logger.Info("adding");
                        _clientBuffers[audio.ClientGuid].AddRange(audio.PcmAudio);
                    }
                }
            }
        }

        internal bool IsReady()
        {
            var diff = GetTickCount64() - _firstPacketTime;

            //TODO check this? maybe tune 
            if (diff >= _bufferLength)
            {
                _firstPacketTime = int.MaxValue;
                return true;
            }
            return false;
        }


        internal byte[] MixDown()
        {
            _firstPacketTime = long.MaxValue;

            var mixDownSize = 0;
            var largestIndex = 0;

            if (_clientBuffers.Count() > 1)
            {
                var clientBytesArray = _clientBuffers.Values.ToList();


                for (var i = 0; i < clientBytesArray.Count(); i++)
                {
                    var client = clientBytesArray[i];
                    if (client.Count() > mixDownSize)
                    {
                        mixDownSize = client.Count();
                        largestIndex = i;
                    }
                }

                //    return  MixBytes_16Bit(clientBytesArray, mixDownSize).ToArray();

                //copy the longest element from the array out to mix down to start the process off


                var mixDownByteArray = clientBytesArray[largestIndex].ToArray();

                //removed already merged array from list
                clientBytesArray.RemoveAt(largestIndex);
                try
                {
                    for (var i = 0; i < clientBytesArray.Count(); i++)
                    {
                        var speaker1Bytes = clientBytesArray[i].ToArray();
                        //     var speaker2Bytes = clientBytesArray[i+1].ToArray();

                        var limit = speaker1Bytes.Count();


                        limit = limit/2;

                        for (int j = 0, offset = 0; j < limit; j++, offset += 2)
                        {
                            var speaker1Short = BitConverter.ToInt16(speaker1Bytes, offset);
                            var speaker2Short = BitConverter.ToInt16(mixDownByteArray, offset);

                            var mixdown = BitConverter.GetBytes(MixSpeakers(speaker1Short, speaker2Short));

                            mixDownByteArray[offset] = mixdown[0];
                            mixDownByteArray[offset + 1] = mixdown[1];
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Error processing audio mixdown ");
                }

                _clientBuffers.Clear();
                return mixDownByteArray;
            }
            if (_clientBuffers.Count() == 1)
            {
                var res = _clientBuffers.Values.First().ToArray();
                _clientBuffers.Clear();
                return res;
            }

            return new byte[0];
        }


        //FROM: http://stackoverflow.com/a/25102339

        private short MixSpeakers(int speaker1, int speaker2)
        {
            return (short) (speaker1 + speaker2 - speaker1*speaker2/65535);

            //method 2
            //int tmp = speaker1 + speaker2;
            //return (short)(tmp / 2);


            //method 3
            //float samplef1 = speaker1 / 32768.0f;
            //float samplef2 = speaker2 / 32768.0f;
            //float mixed = samplef1 + samplef2;
            //// reduce the volume a bit:
            //mixed *= 0.8f;
            //// hard clipping
            //if (mixed > 1.0f) mixed = 1.0f;
            //if (mixed < -1.0f) mixed = -1.0f;
            //short outputSample = (short)(mixed * 32768.0f);
            //return outputSample;

            //method 4
            //int m; // mixed result will go here

            //// Make both samples unsigned (0..65535)
            //speaker1 += 32768;
            //speaker2 += 32768;

            //// Pick the equation
            //if ((speaker1 < 32768) || (speaker2 < 32768))
            //{
            //    // Viktor's first equation when both sources are "quiet"
            //    // (i.e. less than middle of the dynamic range)
            //    m = speaker1 * speaker2 / 32768;
            //}
            //else {
            //    // Viktor's second equation when one or both sources are loud
            //    m = 2 * (speaker1 + speaker2) - (speaker1 * speaker2) / 32768 - 65536;
            //}

            //// Output is unsigned (0..65536) so convert back to signed (-32768..32767)
            //if (m == 65536)
            //    m = 65535;

            //m -= 32768;

            //return (short)m;
        }
    }
}