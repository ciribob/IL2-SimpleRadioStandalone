using System;
using System.Net;
using System.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.ExternalAudio.Audio;
using Ciribob.IL2.SimpleRadio.Standalone.ExternalAudio.Models;
using Easy.MessageHub;
using NLog;
using Timer = Cabhishek.Timers.Timer;

namespace Ciribob.IL2.SimpleRadio.Standalone.ExternalAudio
{
    internal class ExternalAudioClient
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private string mp3Path;
        private double freq;
        private string modulation;
        private int coalition;
        private readonly int port;

        private readonly string Guid = ShortGuid.NewGuid();

        private CancellationTokenSource finished = new CancellationTokenSource();
        private PlayerGameState gameState;
        private UdpVoiceHandler udpVoiceHandler;
        private string name;
        private readonly float volume;

        public ExternalAudioClient(string mp3Path, double freq, string modulation, int coalition, int port, string name, float volume)
        {
            this.mp3Path = mp3Path;
            this.freq = freq;
            this.modulation = modulation;
            this.coalition = coalition;
            this.port = port;
            this.name = name;
            this.volume = volume;
        }

        public void Start()
        {

            MessageHub.Instance.Subscribe<ReadyMessage>(ReadyToSend);
            MessageHub.Instance.Subscribe<DisconnectedMessage>(Disconnected);


            gameState = new PlayerGameState();
            gameState.radios[1].modulation = (RadioInformation.Modulation)(modulation == "AM" ? 0 : 1);
            gameState.radios[1].freq = this.freq * 1000000; // get into Hz
            gameState.radios[1].name = name;
            gameState.coalition = (short) this.coalition;

            Logger.Info($"Starting with params:");
            Logger.Info($"Path or Text to Say: {mp3Path} ");
            Logger.Info($"Frequency: {gameState.radios[1].freq} Hz ");
            Logger.Info($"Modulation: {gameState.radios[1].modulation} ");
            Logger.Info($"Coalition: {coalition} ");
            Logger.Info($"IP: 127.0.0.1 ");
            Logger.Info($"Port: {port} ");
            Logger.Info($"Client Name: {name} ");
            Logger.Info($"Volume: {volume} ");

            var srsClientSyncHandler = new SRSClientSyncHandler(Guid, gameState,name);

            srsClientSyncHandler.TryConnect(new IPEndPoint(IPAddress.Loopback, port));

            //wait for it to end
            finished.Token.WaitHandle.WaitOne();

            Logger.Info("Finished - Closing");

            udpVoiceHandler?.RequestStop();
            srsClientSyncHandler?.Disconnect();

            MessageHub.Instance.ClearSubscriptions();
        }

        private void ReadyToSend(ReadyMessage ready)
        {
            if (udpVoiceHandler == null)
            {
                Logger.Info($"Connecting UDP VoIP");
                udpVoiceHandler = new UdpVoiceHandler(Guid, IPAddress.Loopback, port, gameState);
                udpVoiceHandler.Start();
                new Thread(SendAudio).Start();
            }
        }

        private void Disconnected(DisconnectedMessage disconnected)
        {
            finished.Cancel();
        }

        private void SendAudio()
        {
            Logger.Info("Sending Audio... Please Wait");
            AudioGenerator mp3 = new AudioGenerator(mp3Path, volume);
            var opusBytes = mp3.GetOpusBytes();
            int count = 0;

            CancellationTokenSource tokenSource = new CancellationTokenSource();

            //get all the audio as Opus frames of 40 ms
            //send on 40 ms timer 

            //when empty - disconnect
            //user timer for accurate sending
            var _timer = new Timer(() =>
            {

                if (!finished.IsCancellationRequested)
                {
                    if (count < opusBytes.Count)
                    {
                        udpVoiceHandler.Send(opusBytes[count], opusBytes[count].Length);
                        count++;

                        if (count % 50 == 0)
                        {
                            Logger.Info($"Playing audio - sent {count * 40}ms - {((float)count / (float)opusBytes.Count) * 100.0:F0}% ");
                        }
                    }
                    else
                    {
                        tokenSource.Cancel();
                    }
                }
                else
                {
                    Logger.Error("Client Disconnected");
                    tokenSource.Cancel();
                    return;
                }

            }, TimeSpan.FromMilliseconds(40));
            _timer.Start();

            //wait for cancel
            tokenSource.Token.WaitHandle.WaitOne();
            _timer.Stop();

            Logger.Info("Finished Sending Audio");
            finished.Cancel();
        }
    }
}
