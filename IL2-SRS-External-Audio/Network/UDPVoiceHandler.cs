using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using NLog;
using static Ciribob.IL2.SimpleRadio.Standalone.Common.RadioInformation;
using Timer = Cabhishek.Timers.Timer;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network
{
    internal class UdpVoiceHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IPAddress _address;

        private readonly byte[] _guidAsciiBytes;
        private readonly CancellationTokenSource _pingStop = new CancellationTokenSource();
        private readonly int _port;
        private readonly PlayerGameState gameState;

        private readonly CancellationTokenSource _stopFlag = new CancellationTokenSource();

        //    private readonly JitterBuffer _jitterBuffer = new JitterBuffer();
        private UdpClient _listener;

        private ulong _packetNumber = 1;

        private IPEndPoint _serverEndpoint;

        private volatile bool _stop;



        public UdpVoiceHandler(string guid, IPAddress address, int port, PlayerGameState gameState)
        {
            _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

            _address = address;
            _port = port;
            this.gameState = gameState;

            _serverEndpoint = new IPEndPoint(_address, _port);
        }

     
        public void Start()
        {
            _listener = new UdpClient();
            try
            {
                _listener.AllowNatTraversal(true);
            }
            catch { }

            _packetNumber = 1; //reset packet number


            byte[] message = _guidAsciiBytes;

            Logger.Info($"Sending UDP Ping");
            // Force immediate ping once to avoid race condition before starting to listen
            _listener.Send(message, message.Length, _serverEndpoint);

            Thread.Sleep(3000);
            Logger.Info($"Ping Sent");
        }


        public void RequestStop()
        {
            _stop = true;
            try
            {
                _listener.Close();
            }
            catch (Exception e)
            {
            }

            _stopFlag.Cancel();
        }

        public bool Send(byte[] bytes, int len)
        {
            
            if (!_stop
                && _listener != null
                && (bytes != null))
                //can only send if IL2 is connected
            {
                try
                {
                    //generate packet
                        var udpVoicePacket = new UDPVoicePacket
                        {
                            GuidBytes = _guidAsciiBytes,
                            AudioPart1Bytes = bytes,
                            AudioPart1Length = (ushort) bytes.Length,
                            Frequencies =new[] {gameState.radios[1].freq},
                            UnitId = gameState.unitId,
                            Modulations = new [] {(byte)gameState.radios[1].modulation},
                            PacketNumber = _packetNumber++,
                            OriginalClientGuidBytes = _guidAsciiBytes
                        };

                        var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

                        _listener?.Send(encodedUdpVoicePacket, encodedUdpVoicePacket.Length, new IPEndPoint(_address, _port));

                    
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Sending Audio Message " + e.Message);
                }
            }
            else
            {
               //couldnt send
            }

            return false;
        }

    }
}
