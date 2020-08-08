using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using FragLabs.Audio.Codecs;
using NLog;
using static Ciribob.IL2.SimpleRadio.Standalone.Common.RadioInformation;
using Timer = Cabhishek.Timers.Timer;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network
{
    internal class UdpVoiceHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly IPAddress _address;
        private readonly AudioManager _audioManager;
        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
        private readonly AudioInputSingleton _audioInputSingleton = AudioInputSingleton.Instance;

        private readonly BlockingCollection<byte[]> _encodedAudio = new BlockingCollection<byte[]>();
        private readonly string _guid;
        private readonly byte[] _guidAsciiBytes;
        private readonly InputDeviceManager _inputManager;
        private readonly CancellationTokenSource _pingStop = new CancellationTokenSource();
        private readonly int _port;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

        private readonly CancellationTokenSource _stopFlag = new CancellationTokenSource();

        private readonly int UDP_VOIP_TIMEOUT = 42; // seconds for timeout before redoing VoIP

        private readonly int JITTER_BUFFER = 50; //in milliseconds

        private ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        //    private readonly JitterBuffer _jitterBuffer = new JitterBuffer();
        private UdpClient _listener;

        private ulong _packetNumber = 1;

        private volatile bool _ptt;
        private long _lastPTTPress; // to handle dodgy PTT - release time

        private volatile bool _ready;

        private IPEndPoint _serverEndpoint;

        private volatile bool _stop;

        private Timer _timer;

        private long _udpLastReceived = 0;
        private DispatcherTimer _updateTimer;

        private RadioReceivingState[] _radioReceivingState;

        public UdpVoiceHandler(string guid, IPAddress address, int port, AudioManager audioManager,
            InputDeviceManager inputManager)
        {
            _radioReceivingState = _clientStateSingleton.RadioReceivingState;

            _audioManager = audioManager;
            _guidAsciiBytes = Encoding.ASCII.GetBytes(guid);

            _guid = guid;
            _address = address;
            _port = port;

            _serverEndpoint = new IPEndPoint(_address, _port);

            _inputManager = inputManager;

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _updateTimer.Tick += UpdateVOIPStatus;
            _updateTimer.Start();

            
        }

        private void UpdateVOIPStatus(object sender, EventArgs e)
        {
            TimeSpan diff = TimeSpan.FromTicks(DateTime.Now.Ticks - _udpLastReceived);

            //ping every 10 so after 40 seconds VoIP UDP issue
            if (diff.TotalSeconds > UDP_VOIP_TIMEOUT)
            {
                _clientStateSingleton.IsVoipConnected = false;
            }
            else
            {
                _clientStateSingleton.IsVoipConnected = true;
            }
        }

        private void AudioEffectCheckTick()
        {


            for (var i = 0; i < _radioReceivingState.Length; i++)
            {
                //Nothing on this radio!
                //play out if nothing after 200ms
                //and Audio hasn't been played already
                var radioState = _radioReceivingState[i];
                if ((radioState != null) && !radioState.PlayedEndOfTransmission && !radioState.IsReceiving)
                {
                    radioState.PlayedEndOfTransmission = true;

                    var radioInfo = _clientStateSingleton.PlayerGameState;
                    _audioManager.PlaySoundEffectEndReceive(i, radioInfo.radios[i].volume, radioInfo.radios[i].modulation);
                }
            }
        }

        public void Listen()
        {
            _udpLastReceived = 0;
            _ready = false;
            _listener = new UdpClient();
            try
            {
                _listener.AllowNatTraversal(true);
            }
            catch { }
            // _listener.Connect(_serverEndpoint);

            //start 2 audio processing threads
            var decoderThread = new Thread(UdpAudioDecode);
            decoderThread.Start();

            var settings = GlobalSettingsStore.Instance;
            _inputManager.StartDetectPtt(pressed =>
            {
                var radios = _clientStateSingleton.PlayerGameState;

                var radioSwitchPtt = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT);
                var radioSwitchPttWhenValid = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTTOnlyWhenValid);
                
                //store the current PTT state and radios
                var currentRadioId = radios.selected;
                var currentPtt = _ptt;
                
                var ptt = false;
                foreach (var inputBindState in pressed)
                {
                    if (inputBindState.IsActive)
                    {
                        //radio switch?
                        if ((int) inputBindState.MainDevice.InputBind >= (int) InputBinding.Intercom &&
                            (int) inputBindState.MainDevice.InputBind <= (int) InputBinding.Switch10)
                        {
                            //gives you radio id if you minus 100
                            var radioId = (int) inputBindState.MainDevice.InputBind - 100;

                            if (radioId < _clientStateSingleton.PlayerGameState.radios.Length)
                            {
                                var clientRadio = _clientStateSingleton.PlayerGameState.radios[radioId];

                                if (RadioHelper.SelectRadio(radioId, !(radioSwitchPttWhenValid || radioSwitchPtt)))
                                {
                                    //turn on PTT
                                    if (radioSwitchPttWhenValid || radioSwitchPtt)
                                    {
                                        _lastPTTPress = DateTime.Now.Ticks;
                                        ptt = true;
                                        //Store last release time
                                    }
                                }
                                else
                                {
                                    //turn on PTT even if not valid radio switch
                                    if (radioSwitchPtt)
                                    {
                                        _lastPTTPress = DateTime.Now.Ticks;
                                        ptt = true;
                                    }
                                }

                            }
                        }
                        else if (inputBindState.MainDevice.InputBind == InputBinding.Ptt)
                        {
                            _lastPTTPress = DateTime.Now.Ticks;
                            ptt = true;
                        }
                    }
                }

                //if length is zero - no keybinds or no PTT pressed set to false
                var diff = new TimeSpan(DateTime.Now.Ticks - _lastPTTPress);

                //Release the PTT ONLY if X ms have passed and we didnt switch radios to handle
                //shitty buttons
                var releaseTime = _globalSettings.ProfileSettingsStore
                    .GetClientSetting(ProfileSettingsKeys.PTTReleaseDelay).IntValue;
                
                if (!ptt
                    && releaseTime > 0
                    && diff.TotalMilliseconds <= releaseTime
                    && currentRadioId == radios.selected)
                {
                        ptt = true;
                }

                _ptt = ptt;
            });

            StartTimer();

            StartPing();

            _packetNumber = 1; //reset packet number
            
            while (!_stop)
            {
               if(_ready)
                {
                    try
                    {
                        var groupEp = new IPEndPoint(IPAddress.Any, _port);
                        //   listener.Client.ReceiveTimeout = 3000;

                        var bytes = _listener.Receive(ref groupEp);

                        if (bytes?.Length == 22)
                        {
                            _udpLastReceived = DateTime.Now.Ticks;
                            Logger.Info("Received Ping Back from Server");
                        }
                        else if (bytes?.Length > 22)
                        {
                            _udpLastReceived = DateTime.Now.Ticks;
                            _encodedAudio.Add(bytes);
                        }
                    }
                    catch (Exception e)
                    {
                        //  logger.Error(e, "error listening for UDP Voip");
                    }
                }
            }

            _ready = false;

            //stop UI Refreshing
            _updateTimer.Stop();

            _clientStateSingleton.IsVoipConnected = false;
        }

        public void StartTimer()
        {
            StopTimer();

            // _jitterBuffer.Clear();
            _timer = new Timer(AudioEffectCheckTick, TimeSpan.FromMilliseconds(JITTER_BUFFER));
            _timer.Start();
        }

        public void StopTimer()
        {
            if (_timer != null)
            {
                //    _jitterBuffer.Clear();
                _timer.Stop();
                _timer = null;
            }
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
            _pingStop.Cancel();

            _inputManager.StopPtt();

            StopTimer();
        }

        private SRClient IsClientMetaDataValid(string clientGuid)
        {
            if (_clients.ContainsKey(clientGuid))
            {
                var client = _clients[_guid];

                if (client != null)
                {
                    return client;
                }
            }

            return null;
        }

        private void UdpAudioDecode()
        {
            try
            {
                while (!_stop)
                {
                    try
                    {
                        var encodedOpusAudio = new byte[0];
                        _encodedAudio.TryTake(out encodedOpusAudio, 100000, _stopFlag.Token);

                        var time = DateTime.Now.Ticks; //should add at the receive instead?

                        if ((encodedOpusAudio != null)
                            && (encodedOpusAudio.Length >=
                                (UDPVoicePacket.PacketHeaderLength + UDPVoicePacket.FixedPacketLength +
                                 UDPVoicePacket.FrequencySegmentLength)))
                        {
                            //  process
                            // check if we should play audio

                            var myClient = IsClientMetaDataValid(_guid);

                            if ((myClient != null))
                            {
                                //Decode bytes
                                var udpVoicePacket = UDPVoicePacket.DecodeVoicePacket(encodedOpusAudio);

                                if (udpVoicePacket != null)
                                {
                                    var vehicleId = -1;
                                    if (_clients.TryGetValue(udpVoicePacket.Guid, out var transmittingClient))
                                    {
                                        vehicleId = transmittingClient.GameState.vehicleId;
                                    }

                                    var globalFrequencies = _serverSettings.GlobalFrequencies;

                                    var frequencyCount = udpVoicePacket.Frequencies.Length;

                                    List<RadioReceivingPriority> radioReceivingPriorities =
                                        new List<RadioReceivingPriority>(frequencyCount);
                                    List<int> blockedRadios = CurrentlyBlockedRadios();

                                    // Parse frequencies into receiving radio priority for selection below
                                    for (var i = 0; i < frequencyCount; i++)
                                    {
                                        RadioReceivingState state = null;
                                        bool decryptable;

                                        //Check if Global
                                        bool globalFrequency = globalFrequencies.Contains(udpVoicePacket.Frequencies[i]);


                                        var radio = _clientStateSingleton.PlayerGameState.CanHearTransmission(
                                            udpVoicePacket.Frequencies[i],
                                            (RadioInformation.Modulation) udpVoicePacket.Modulations[i],
                                            udpVoicePacket.UnitId,
                                            vehicleId,
                                            blockedRadios,
                                            out state);

                                        float losLoss = 0.0f;
                                        double receivPowerLossPercent = 0.0;

                                        if (radio != null && state != null)
                                        {
                                            if (
                                                radio.modulation == RadioInformation.Modulation.INTERCOM
                                                || globalFrequency
                                                || (!blockedRadios.Contains(state.ReceivedOn)
                                                )
                                            )
                                            {

                                                radioReceivingPriorities.Add(new RadioReceivingPriority()
                                                {
                                                    Frequency = udpVoicePacket.Frequencies[i],
                                                    Modulation = udpVoicePacket.Modulations[i],
                                                    ReceivingRadio = radio,
                                                    ReceivingState = state
                                                });
                                            }
                                        }
                                    }

                                    // Sort receiving radios to play audio on correct one
                                    radioReceivingPriorities.Sort(SortRadioReceivingPriorities);

                                    if (radioReceivingPriorities.Count > 0)
                                    {
                                        

                                        //ALL GOOD!
                                        //create marker for bytes
                                        for (int i = 0; i < radioReceivingPriorities.Count; i++)
                                        {
                                            var destinationRadio = radioReceivingPriorities[i];
                                            var isSimultaneousTransmission = radioReceivingPriorities.Count > 1 && i > 0;

                                            var audio = new ClientAudio
                                            {
                                                ClientGuid = udpVoicePacket.Guid,
                                                EncodedAudio = udpVoicePacket.AudioPart1Bytes,
                                                //Convert to Shorts!
                                                ReceiveTime = DateTime.Now.Ticks,
                                                Frequency = destinationRadio.Frequency,
                                                Modulation = destinationRadio.Modulation,
                                                Volume = destinationRadio.ReceivingRadio.volume,
                                                ReceivedRadio = destinationRadio.ReceivingState.ReceivedOn,
                                                UnitId = udpVoicePacket.UnitId,
                                            
                                                RadioReceivingState = destinationRadio.ReceivingState,
                                                PacketNumber = udpVoicePacket.PacketNumber,
                                                OriginalClientGuid = udpVoicePacket.OriginalClientGuid
                                            };


                                            //handle effects
                                            var radioState = _radioReceivingState[audio.ReceivedRadio];

                                            if (!isSimultaneousTransmission &&
                                                (radioState == null || radioState.PlayedEndOfTransmission ||
                                                 !radioState.IsReceiving))
                                            {
                                                
                                                _audioManager.PlaySoundEffectStartReceive(audio.ReceivedRadio,
                                                    false,
                                                    audio.Volume,(RadioInformation.Modulation) audio.Modulation);
                                                
                                            }

                                            var transmitterName = "";
                                            if (_serverSettings.GetSettingAsBool(ServerSettingsKeys.SHOW_TRANSMITTER_NAME)
                                                && _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ShowTransmitterName)
                                                && transmittingClient!=null)

                                            {
                                                transmitterName = transmittingClient.Name;
                                            }

                                            var newRadioReceivingState =  new RadioReceivingState
                                            {
                                                IsSecondary = destinationRadio.ReceivingState.IsSecondary,
                                                LastReceivedAt = DateTime.Now.Ticks,
                                                PlayedEndOfTransmission = false,
                                                ReceivedOn = destinationRadio.ReceivingState.ReceivedOn,
                                                SentBy = transmitterName
                                            };

                                            _radioReceivingState[audio.ReceivedRadio] = newRadioReceivingState;

                                            // Only play actual audio once
                                            if (i == 0)
                                            {
                                                _audioManager.AddClientAudio(audio);
                                            }
                                        }

                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!_stop)
                        {
                            Logger.Info(ex, "Failed to decode audio from Packet");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Stopped DeJitter Buffer");
            }
        }

        private List<int> CurrentlyBlockedRadios()
        {
            List<int> transmitting = new List<int>();
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX))
            {
                return transmitting;
            }

            if (!_ptt && !_clientStateSingleton.PlayerGameState.ptt)
            {
                return transmitting;
            }

            //Currently transmitting - PTT must be true - figure out if we can hear on those radios

            var currentRadio = _clientStateSingleton.PlayerGameState.radios[_clientStateSingleton.PlayerGameState.selected];

            if (currentRadio.modulation == RadioInformation.Modulation.FM 
                || currentRadio.modulation == RadioInformation.Modulation.AM)
            {
                //only AM and FM block - SATCOM etc dont

                transmitting.Add(_clientStateSingleton.PlayerGameState.selected);
            }

            return transmitting;
        }


        private int SortRadioReceivingPriorities(RadioReceivingPriority x, RadioReceivingPriority y)
        {
            int xScore = 0;
            int yScore = 0;

            if (x.ReceivingRadio == null || x.ReceivingState == null)
            {
                return 1;
            }

            if (y.ReceivingRadio == null | y.ReceivingState == null)
            {
                return -1;
            }

            if (_clientStateSingleton.PlayerGameState.selected == x.ReceivingState.ReceivedOn)
            {
                xScore += 8;
            }

            if (_clientStateSingleton.PlayerGameState.selected == y.ReceivingState.ReceivedOn)
            {
                yScore += 8;
            }

            if (x.ReceivingRadio.volume > 0)
            {
                xScore += 4;
            }

            if (y.ReceivingRadio.volume > 0)
            {
                yScore += 4;
            }

            return yScore - xScore;
        }

        private List<RadioInformation> PTTPressed(out int sendingOn)
        {
            sendingOn = -1;
            

            var radioInfo = _clientStateSingleton.PlayerGameState;
            //If its a hot intercom and thats not the currently selected radio
            //this is special logic currently for the gazelle as it has a hot mic, but no way of knowing if you're transmitting from the module itself
            //so we have to figure out what you're transmitting on in SRS
            if (radioInfo.intercomHotMic 
                && radioInfo.control == PlayerGameState.RadioSwitchControls.IN_COCKPIT
                && radioInfo.selected != 0 && !_ptt && !radioInfo.ptt)
            {
                if (radioInfo.radios[0].modulation == RadioInformation.Modulation.INTERCOM)
                {
                    var intercom = new List<RadioInformation>();
                    intercom.Add(radioInfo.radios[0]);
                    sendingOn = 0;
                    return intercom;
                }
            }

            var transmittingRadios = new List<RadioInformation>();
            if (_ptt || _clientStateSingleton.PlayerGameState.ptt)
            {
                // Always add currently selected radio (if valid)
                var currentSelected = _clientStateSingleton.PlayerGameState.selected;
                RadioInformation currentlySelectedRadio = null;
                if (currentSelected >= 0
                    && currentSelected < _clientStateSingleton.PlayerGameState.radios.Length)
                {
                    currentlySelectedRadio = _clientStateSingleton.PlayerGameState.radios[currentSelected];

                    if (currentlySelectedRadio != null && currentlySelectedRadio.modulation !=
                                                       RadioInformation.Modulation.DISABLED
                                                       && (currentlySelectedRadio.freq > 100 ||
                                                           currentlySelectedRadio.modulation ==
                                                           RadioInformation.Modulation.INTERCOM))
                    {
                        sendingOn = currentSelected;
                        transmittingRadios.Add(currentlySelectedRadio);
                    }
                }

            }

            return transmittingRadios;
        }

        public bool Send(byte[] bytes, int len)
        {
            // List of radios the transmission is sent to (can me multiple if simultaneous transmission is enabled)
            List<RadioInformation> transmittingRadios;
            //if either PTT is true, a microphone is available && socket connected etc
            var sendingOn = -1;
            if (_ready
                && _listener != null
                && _audioInputSingleton.MicrophoneAvailable
                && (bytes != null)
                && (transmittingRadios = PTTPressed(out sendingOn)).Count >0 )
                //can only send if IL2 is connected
            {
                try
                {

                    if (transmittingRadios.Count > 0)
                    {
                        List<double> frequencies = new List<double>(transmittingRadios.Count);
                        List<byte> encryptions = new List<byte>(transmittingRadios.Count);
                        List<byte> modulations = new List<byte>(transmittingRadios.Count);

                        for (int i = 0; i < transmittingRadios.Count; i++)
                        {
                            var radio = transmittingRadios[i];

                            // Further deduplicate transmitted frequencies if they have the same freq./modulation/encryption (caused by differently named radios)
                            bool alreadyIncluded = false;
                            for (int j = 0; j < frequencies.Count; j++)
                            {
                                if (frequencies[j] == radio.freq
                                    && modulations[j] == (byte) radio.modulation)
                                {
                                    alreadyIncluded = true;
                                    break;
                                }
                            }

                            if (alreadyIncluded)
                            {
                                continue;
                            }

                            frequencies.Add(radio.freq);
                            encryptions.Add( (byte) 0);
                            modulations.Add((byte) radio.modulation);
                        }

                        //generate packet
                        var udpVoicePacket = new UDPVoicePacket
                        {
                            GuidBytes = _guidAsciiBytes,
                            AudioPart1Bytes = bytes,
                            AudioPart1Length = (ushort) bytes.Length,
                            Frequencies = frequencies.ToArray(),
                            UnitId = _clientStateSingleton.PlayerGameState.unitId,
                            Modulations = modulations.ToArray(),
                            PacketNumber = _packetNumber++,
                            OriginalClientGuidBytes = _guidAsciiBytes
                        };

                        var encodedUdpVoicePacket = udpVoicePacket.EncodePacket();

                        _listener.Send(encodedUdpVoicePacket, encodedUdpVoicePacket.Length, new IPEndPoint(_address, _port));

                        var currentlySelectedRadio = _clientStateSingleton.PlayerGameState.radios[sendingOn];

                        //not sending or really quickly switched sending
                        if (currentlySelectedRadio != null &&
                            (!_clientStateSingleton.RadioSendingState.IsSending || _clientStateSingleton.RadioSendingState.SendingOn != sendingOn))
                        {
                            _audioManager.PlaySoundEffectStartTransmit(sendingOn,
                                currentlySelectedRadio.volume, currentlySelectedRadio.modulation);
                        }

                        //set radio overlay state
                        _clientStateSingleton.RadioSendingState = new RadioSendingState
                        {
                            IsSending = true,
                            LastSentAt = DateTime.Now.Ticks,
                            SendingOn = sendingOn
                        };
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception Sending Audio Message " + e.Message);
                }
            }
            else
            {
                if (_clientStateSingleton.RadioSendingState.IsSending)
                {
                    _clientStateSingleton.RadioSendingState.IsSending = false;

                    if (_clientStateSingleton.RadioSendingState.SendingOn >= 0)
                    {
                        var radio = _clientStateSingleton.PlayerGameState.radios[_clientStateSingleton.RadioSendingState.SendingOn];

                        _audioManager.PlaySoundEffectEndTransmit(_clientStateSingleton.RadioSendingState.SendingOn, radio.volume, radio.modulation);
                    }
                }
            }

            return false;
        }

        private void StartPing()
        {
            Logger.Info("Pinging Server - Starting");

            byte[] message = _guidAsciiBytes;

            // Force immediate ping once to avoid race condition before starting to listen
            _listener.Send(message, message.Length, _serverEndpoint);

            var thread = new Thread(() =>
            {
                //wait for initial sync - then ping
                if (_pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(2)))
                {
                    return;
                }

                _ready = true;

                while (!_stop)
                {
                    //Logger.Info("Pinging Server");
                    try
                    {
                        if (_listener != null)
                        {
                            _listener.Send(message, message.Length,_serverEndpoint);
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Sending Audio Ping! " + e.Message);
                    }

                    //wait for cancel or quit
                    var cancelled = _pingStop.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(15));

                    if (cancelled)
                    {
                        return;
                    }

                    TimeSpan diff = TimeSpan.FromTicks(DateTime.Now.Ticks - _udpLastReceived);

                    //reconnect to UDP - port is no good!
                    if (diff.TotalSeconds > UDP_VOIP_TIMEOUT)
                    {
                        Logger.Error("VoIP Timeout - Recreating VoIP Connection");
                        _ready = false;
                        try
                        {
                            _listener?.Close();
                        }catch(Exception ex)
                        { }

                        _listener = null;

                        _udpLastReceived = 0;

                        _listener = new UdpClient();
                        try
                        {
                            _listener.AllowNatTraversal(true);
                        }
                        catch { }

                        try
                        {
                            // Force immediate ping once to avoid race condition before starting to listen
                            _listener.Send(message, message.Length, _serverEndpoint);
                            _ready = true;
                            Logger.Error("VoIP Timeout - Success Recreating VoIP Connection");
                        }
                        catch (Exception e) {
                            Logger.Error(e, "Exception Sending Audio Ping! " + e.Message);
                        }
                        
                    }
                   
                }
            });
            thread.Start();
        }
    }
}