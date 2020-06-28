using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using System.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2
{
    public class IL2RadioSyncHandler
    {
        private readonly IL2RadioSyncManager.SendRadioUpdate _radioUpdate;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;

        private static readonly int RADIO_UPDATE_PING_INTERVAL = 60; //send update regardless of change every X seconds


        private UdpClient _il2UdpListener;
        private UdpClient _il2RadioUpdateSender;

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

        private volatile bool _stop;

        public delegate void NewAircraft(string name);

        private readonly NewAircraft _newAircraftCallback;

        private long _identStart = 0;

        public IL2RadioSyncHandler(IL2RadioSyncManager.SendRadioUpdate radioUpdate, NewAircraft _newAircraft)
        {
            _radioUpdate = radioUpdate;
            _newAircraftCallback = _newAircraft;
        }

        public void Start()
        {
            //reset last sent
            _clientStateSingleton.LastSent = 0;

            Task.Factory.StartNew(() =>
            {
                while (!_stop)
                {
                    var localEp = new IPEndPoint(IPAddress.Any, _globalSettings.GetNetworkSetting(GlobalSettingsKeys.IL2IncomingUDP));
                    try
                    {
                        _il2UdpListener = new UdpClient(localEp);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Unable to bind to the DCS Export Listener Socket Port: {localEp.Port}");
                        Thread.Sleep(500);
                    }
                }

                while (!_stop)
                {
                    try
                    {
                        var groupEp = new IPEndPoint(IPAddress.Any,0);
                        var bytes = _il2UdpListener.Receive(ref groupEp);

                        var str = Encoding.UTF8.GetString(
                            bytes, 0, bytes.Length).Trim();

                        var message =
                            JsonConvert.DeserializeObject<DCSPlayerRadioInfo>(str);

                        Logger.Debug($"Recevied Message from IL2 {str}");

                        if (!string.IsNullOrWhiteSpace(message.name) && message.name != "Unknown" && message.name != _clientStateSingleton.LastSeenName)
                        {
                            _clientStateSingleton.LastSeenName = message.name;
                        }

                        _clientStateSingleton.DcsExportLastReceived = DateTime.Now.Ticks;

                        //sync with others
                        //Radio info is marked as Stale for FC3 aircraft after every frequency change

                        ProcessRadioInfo(message);
                    }
                    catch (SocketException e)
                    {
                        // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                        if (!_stop)
                        {
                            Logger.Error(e, "SocketException Handling DCS Message");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Handling DCS Message");
                    }
                }

                try
                {
                    _il2UdpListener.Close();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception stoping DCS listener ");
                }
                
            });
        }


        public void ProcessRadioInfo(DCSPlayerRadioInfo message)
        {
          
            // determine if its changed by comparing old to new
            var update = UpdateRadio(message);

            //send to DCS UI
            SendRadioUpdateToDCS();

            Logger.Debug("Update sent to DCS");

            var diff = new TimeSpan( DateTime.Now.Ticks - _clientStateSingleton.LastSent);

            if (update 
                || _clientStateSingleton.LastSent < 1 
                || diff.TotalSeconds > 60)
            {
                Logger.Debug("Sending Radio Info To Server - Update");
                _clientStateSingleton.LastSent = DateTime.Now.Ticks;
                _radioUpdate();
            }
        }

        //send updated radio info back to DCS for ingame GUI
        private void SendRadioUpdateToDCS()
        {
            if (_il2RadioUpdateSender == null)
            {
                _il2RadioUpdateSender = new UdpClient();
            }

            try
            {
                var connectedClientsSingleton = ConnectedClientsSingleton.Instance;
                int[] tunedClients = new int[11];

                if (_clientStateSingleton.IsConnected
                    && _clientStateSingleton.DcsPlayerRadioInfo !=null
                    && _clientStateSingleton.DcsPlayerRadioInfo.IsCurrent())
                {

                    for (int i = 0; i < tunedClients.Length; i++)
                    {
                        var clientRadio = _clientStateSingleton.DcsPlayerRadioInfo.radios[i];
                        
                        if (clientRadio.modulation != RadioInformation.Modulation.DISABLED)
                        {
                            tunedClients[i] = connectedClientsSingleton.ClientsOnFreq(clientRadio.freq, clientRadio.modulation);
                        }
                    }
                }
                
                //get currently transmitting or receiving
                var combinedState = new CombinedRadioState()
                {
                    RadioInfo = _clientStateSingleton.DcsPlayerRadioInfo,
                    RadioSendingState = _clientStateSingleton.RadioSendingState,
                    RadioReceivingState = _clientStateSingleton.RadioReceivingState,
                    ClientCountConnected = _clients.Total,
                    TunedClients = tunedClients,
                };

                var message = JsonConvert.SerializeObject(combinedState, new JsonSerializerSettings
                {
                 //   NullValueHandling = NullValueHandling.Ignore,
                    ContractResolver = new JsonDCSPropertiesResolver(),
                }) + "\n";

                var byteData =
                    Encoding.UTF8.GetBytes(message);

                //Logger.Info("Sending Update over UDP 7080 DCS - 7082 Flight Panels: \n"+message);

                _il2RadioUpdateSender.Send(byteData, byteData.Length,
                    new IPEndPoint(IPAddress.Parse("127.0.0.1"),
                        _globalSettings.GetNetworkSetting(GlobalSettingsKeys.OutgoingDCSUDPInfo))); //send to DCS
            }
            catch (Exception e)
            {
                Logger.Error(e, "Exception Sending DCS Radio Update Message");
            }
        }

        private bool UpdateRadio(DCSPlayerRadioInfo message)
        {
            var expansion = _serverSettings.GetSettingAsBool(ServerSettingsKeys.RADIO_EXPANSION);

            var playerRadioInfo = _clientStateSingleton.DcsPlayerRadioInfo;

            //copy and compare to look for changes
            var beforeUpdate = playerRadioInfo.DeepClone();

            //update common parts
            playerRadioInfo.name = message.name;
            playerRadioInfo.inAircraft = message.inAircraft;
            playerRadioInfo.intercomHotMic = message.intercomHotMic;

            if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AlwaysAllowHotasControls))
            {
                message.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
                playerRadioInfo.control = DCSPlayerRadioInfo.RadioSwitchControls.HOTAS;
            }
            else
            {
                playerRadioInfo.control = message.control;
            }

            playerRadioInfo.simultaneousTransmissionControl = message.simultaneousTransmissionControl;

            playerRadioInfo.unit = message.unit;

            var overrideFreqAndVol = false;

            var newAircraft = playerRadioInfo.unitId != message.unitId || !playerRadioInfo.IsCurrent();

            if (message.unitId >= DCSPlayerRadioInfo.UnitIdOffset &&
                playerRadioInfo.unitId >= DCSPlayerRadioInfo.UnitIdOffset)
            {
                //overriden so leave as is
            }
            else
            {
                overrideFreqAndVol = playerRadioInfo.unitId != message.unitId;
                playerRadioInfo.unitId = message.unitId;
            }

            if (newAircraft)
            {
                if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoSelectSettingsProfile))
                {
                    _newAircraftCallback(message.unit);
                }

            }

            if (overrideFreqAndVol)
            {
                playerRadioInfo.selected = message.selected;
            }

            if (playerRadioInfo.control == DCSPlayerRadioInfo.RadioSwitchControls.IN_COCKPIT)
            {
                playerRadioInfo.selected = message.selected;
            }

            bool simul = false;


            //copy over radio names, min + max
            for (var i = 0; i < playerRadioInfo.radios.Length; i++)
            {
                var clientRadio = playerRadioInfo.radios[i];

                //if we have more radios than the message has
                if (i >= message.radios.Length)
                {
                    clientRadio.freq = 1;
                    clientRadio.freqMin = 1;
                    clientRadio.freqMax = 1;
                    clientRadio.secFreq = 0;
                    clientRadio.retransmit = false;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";
                    clientRadio.retransmit = false;

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.guardFreqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;

                    continue;
                }

                var updateRadio = message.radios[i];


                if ((updateRadio.expansion && !expansion) ||
                    (updateRadio.modulation == RadioInformation.Modulation.DISABLED))
                {
                    //expansion radio, not allowed
                    clientRadio.freq = 1;
                    clientRadio.freqMin = 1;
                    clientRadio.freqMax = 1;
                    clientRadio.secFreq = 0;
                    clientRadio.retransmit = false;
                    clientRadio.modulation = RadioInformation.Modulation.DISABLED;
                    clientRadio.name = "No Radio";
                    clientRadio.retransmit = false;

                    clientRadio.freqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.guardFreqMode = RadioInformation.FreqMode.COCKPIT;
                    clientRadio.volMode = RadioInformation.VolumeMode.COCKPIT;
                }
                else
                {
                    //update common parts
                    clientRadio.freqMin = updateRadio.freqMin;
                    clientRadio.freqMax = updateRadio.freqMax;

                    clientRadio.name = updateRadio.name;

                    clientRadio.modulation = updateRadio.modulation;

                    //update modes
                    clientRadio.freqMode = updateRadio.freqMode;
                    clientRadio.guardFreqMode = updateRadio.guardFreqMode;


                    clientRadio.volMode = updateRadio.volMode;

                    if ((updateRadio.freqMode == RadioInformation.FreqMode.COCKPIT) || overrideFreqAndVol)
                    {
                        clientRadio.freq = updateRadio.freq;

                        if (newAircraft && updateRadio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                        {
                            //default guard to off
                            clientRadio.secFreq = 0;
                        }
                        else
                        {
                            if (clientRadio.secFreq != 0 && updateRadio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                            {
                                //put back
                                clientRadio.secFreq = updateRadio.secFreq;
                            }
                            else if (clientRadio.secFreq == 0 && updateRadio.guardFreqMode == RadioInformation.FreqMode.OVERLAY)
                            {
                                clientRadio.secFreq = 0;
                            }
                            else
                            {
                                clientRadio.secFreq = updateRadio.secFreq;
                            }

                        }

                        clientRadio.channel = updateRadio.channel;
                    }
                    else
                    {
                        if (clientRadio.secFreq != 0)
                        {
                            //put back
                            clientRadio.secFreq = updateRadio.secFreq;
                        }

                        //check we're not over a limit
                        if (clientRadio.freq > clientRadio.freqMax)
                        {
                            clientRadio.freq = clientRadio.freqMax;
                        }
                        else if (clientRadio.freq < clientRadio.freqMin)
                        {
                            clientRadio.freq = clientRadio.freqMin;
                        }
                    }


                    //handle volume
                    if ((updateRadio.volMode == RadioInformation.VolumeMode.COCKPIT) || overrideFreqAndVol)
                    {
                        clientRadio.volume = updateRadio.volume;
                    }

                    //handle Channels load for radios
                    if (newAircraft && i > 0)
                    {
                        // if (clientRadio.freqMode == RadioInformation.FreqMode.OVERLAY)
                        // {
                        //     var channelModel = _clientStateSingleton.FixedChannels[i - 1];
                        //     channelModel.Reload();
                        //     clientRadio.channel = -1; //reset channel
                        //
                        //     if (_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AutoSelectPresetChannel))
                        //     {
                        //         RadioHelper.RadioChannelUp(i);
                        //     }
                        // }
                        // else
                        // {
                        //     _clientStateSingleton.FixedChannels[i - 1].Clear();
                        //     //clear
                        // }
                    }
                }
            }


            if (playerRadioInfo.simultaneousTransmissionControl ==
                DCSPlayerRadioInfo.SimultaneousTransmissionControl.EXTERNAL_DCS_CONTROL)
            {
                playerRadioInfo.simultaneousTransmission = simul;
            }

            //change PTT last
            if (!_globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AllowIL2PTT))
            {
                playerRadioInfo.ptt = false;
            }
            else
            {
                playerRadioInfo.ptt = message.ptt;
            }

            //                }
            //            }

            //update
            playerRadioInfo.LastUpdate = DateTime.Now.Ticks;

            return !beforeUpdate.Equals(playerRadioInfo);
        }

        public void Stop()
        {
            _stop = true;
            try
            {
                _il2UdpListener?.Close();
            }
            catch (Exception ex)
            {
            }

            try
            {
                _il2RadioUpdateSender?.Close();
            }
            catch (Exception ex)
            {
            }
        }

    }
}
