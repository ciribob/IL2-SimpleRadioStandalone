using System;
using System.Net;
using System.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.ExternalAudio.Audio;
using Ciribob.IL2.SimpleRadio.Standalone.ExternalAudio.Models;
using Easy.MessageHub;

namespace Ciribob.IL2.SimpleRadio.Standalone.ExternalAudio
{
    internal class ExternalAudioClient
    {
        private string mp3Path;
        private double freq;
        private string modulation;
        private int coalition;
        private readonly int port;

        private readonly string Guid = ShortGuid.NewGuid();

        private bool _finished = false;
        private PlayerGameState gameState;
        private UdpVoiceHandler udpVoiceHandler;

        public ExternalAudioClient(string mp3Path, double freq, string modulation, int coalition, int port)
        {
            this.mp3Path = mp3Path;
            this.freq = freq;
            this.modulation = modulation;
            this.coalition = coalition;
            this.port = port;
        }

        public void Start()
        {

            MessageHub.Instance.Subscribe<ReadyMessage>(ReadyToSend);
            

            gameState = new PlayerGameState();
            gameState.radios[1].modulation = (RadioInformation.Modulation)(modulation == "AM" ? 0 : 1);
            gameState.radios[1].freq = this.freq * 1000000; // get into Hz
            gameState.radios[1].name = "Robot";
            gameState.coalition = (short) this.coalition;

            var srsClientSyncHandler = new SRSClientSyncHandler(Guid, gameState);

            srsClientSyncHandler.TryConnect(new IPEndPoint(IPAddress.Loopback, port));

            while (!_finished)
            {
                Thread.Sleep(5000);
            }
            Console.WriteLine("Finished");

            udpVoiceHandler?.RequestStop();
            srsClientSyncHandler?.Disconnect();

            MessageHub.Instance.ClearSubscriptions();
        }

        private void ReadyToSend(ReadyMessage ready)
        {
            if (udpVoiceHandler == null)
            {
                udpVoiceHandler = new UdpVoiceHandler(Guid, IPAddress.Loopback, port, gameState);
                udpVoiceHandler.Start();
                new Thread(SendAudio).Start();
            }
        }

        private void SendAudio()
        {
            Console.WriteLine("Sending Audio");
            MP3OpusReader mp3 = new MP3OpusReader(mp3Path);
            foreach (var opusByte in mp3.GetOpusBytes())
            {
                //can use timer to run through it
                Thread.Sleep(40);

                udpVoiceHandler.Send(opusByte, opusByte.Length);

            }

            //get all the audio as Opus frames of 40 ms
            //send on 40 ms timer 

            //when empty - disconnect

            Console.WriteLine("Finished Audio Buffer");
            _finished = true;

        }
    }
}