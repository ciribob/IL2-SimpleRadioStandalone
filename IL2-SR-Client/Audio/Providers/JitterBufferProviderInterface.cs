using System;
using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Managers;
using NAudio.Utils;
using NAudio.Wave;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Audio
{
    public class JitterBufferProviderInterface : IWaveProvider
    {
        private readonly CircularBuffer _circularBuffer;

        public static readonly int MAXIMUM_BUFFER_SIZE_MS = 2500;

        private readonly byte[] _silence = new byte[AudioManager.SEGMENT_FRAMES * 2]; //*2 for stereo

        private readonly LinkedList<JitterBufferAudio> _bufferedAudio = new LinkedList<JitterBufferAudio>();

        private ulong _lastRead; // gives current index

        private readonly object _lock = new object();

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        //  private const int INITIAL_DELAY_MS = 200;
        //   private long _delayedUntil = -1; //holds audio for a period of time

        public JitterBufferProviderInterface(WaveFormat waveFormat)
        {
            WaveFormat = waveFormat;

            _circularBuffer = new CircularBuffer(WaveFormat.AverageBytesPerSecond * 1); //was 3 

            Array.Clear(_silence, 0, _silence.Length);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            int now = Environment.TickCount;

            //other implementation of waiting
//            if(_delayedUntil > now)
//            {
//                //wait
//                return 0;
//            }

            var read = 0;
            lock (_lock)
            {
                //need to return read equal to count

                //do while loop
                //break when read == count
                //each time round increment read
                //read becomes read + last Read

                do
                {
                    read = read + _circularBuffer.Read(buffer, offset + read, count - read);

                    if (read < count)
                    {
                        //now read in from the jitterbuffer
                        if (_bufferedAudio.Count == 0)
                        {
                            //goes to a mixer so we just return what we've read which could be 0!
                            //Mixer Handles this OK
                            break;
                            //
                            // zero the end of the buffer
                            //      Array.Clear(buffer, offset + read, count - read);
                            //     read = count;
                            //  Console.WriteLine("Buffer Empty");
                        }
                        else
                        {
                            var audio = _bufferedAudio.First.Value;
                            //no Pop?
                            _bufferedAudio.RemoveFirst();

                            if (_lastRead == 0)
                                _lastRead = audio.PacketNumber;
                            else
                            {
                                //TODO deal with looping packet number
                                if (_lastRead + 1 < audio.PacketNumber)
                                {
                                    //fill with missing silence - will only add max of 5x Packet length but it could be a bunch of missing?
                                    var missing = audio.PacketNumber - (_lastRead + 1);

                                    // packet number is always discontinuous at the start of a transmission if you didnt receive a transmission for a while i.e different radio channel
                                    // if the gap is more than 4 assume its just a new transmission

                                    if (missing <= 4)
                                    {
                                        var fill = Math.Min(missing, 4);

                                        for (var i = 0; i < (int)fill; i++)
                                        {
                                            _circularBuffer.Write(_silence, 0, _silence.Length);
                                        }
                                    }
                                  
                                }

                                _lastRead = audio.PacketNumber;
                            }

                            _circularBuffer.Write(audio.Audio, 0, audio.Audio.Length);
                        }
                    }
                } while (read < count);

//                if (read == 0)
//                {
//                    _delayedUntil = Environment.TickCount + INITIAL_DELAY_MS;
//                }
            }

            return read;
        }

        public void AddSamples(JitterBufferAudio jitterBufferAudio)
        {
            lock (_lock)
            {
                //re-order if we can or discard

                //add to linked list
                //add front to back
                if (_bufferedAudio.Count == 0)
                {
                    _bufferedAudio.AddFirst(jitterBufferAudio);
                }
                else if (jitterBufferAudio.PacketNumber > _lastRead)
                { 
                    var time = _bufferedAudio.Count * AudioManager.INPUT_AUDIO_LENGTH_MS; // this isnt quite true as there can be padding audio but good enough

                    if (time > MAXIMUM_BUFFER_SIZE_MS)
                    {
                        _bufferedAudio.Clear();
                        Logger.Warn($"Cleared Audio buffer - length was {time} ms");
                    }

                    for (var it = _bufferedAudio.First; it != null;)
                    {
                        //iterate list
                        //if packetNumber == curentItem
                        // discard
                        //else if packetNumber < _currentItem
                        //add before
                        //else if packetNumber > _currentItem
                        //add before

                        //if not added - add to end?

                        var next = it.Next;

                        if (it.Value.PacketNumber == jitterBufferAudio.PacketNumber)
                        {
                            //discard! Duplicate packet
                            return;
                        }

                        if (jitterBufferAudio.PacketNumber < it.Value.PacketNumber)
                        {
                            _bufferedAudio.AddBefore(it, jitterBufferAudio);
                            return;
                        }

                        if ((jitterBufferAudio.PacketNumber > it.Value.PacketNumber) &&
                            ((next == null) || (jitterBufferAudio.PacketNumber < next.Value.PacketNumber)))
                        {
                            _bufferedAudio.AddAfter(it, jitterBufferAudio);
                            return;
                        }

                        it = next;
                    }
                }
            }
        }
    }
}