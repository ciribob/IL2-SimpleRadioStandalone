using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network
{
    public class UDPCommandHandler
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private UdpClient _udpCommandListener;
        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
        private volatile bool _stop  = false;

        public void Start()
        {
            StartUDPCommandListener();
        }

        private void StartUDPCommandListener()
        {
            

            Task.Factory.StartNew(() =>
            {
                while (!_stop)
                {
                    var localEp = new IPEndPoint(IPAddress.Any, _globalSettings.GetNetworkSetting(GlobalSettingsKeys.CommandListenerUDP));
                    try
                    {
                        _udpCommandListener = new UdpClient(localEp);
                        break;
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(ex, $"Unable to bind to the UDP Command Listener Socket Port: {localEp.Port}");
                        Thread.Sleep(500);
                    }
                }
                
                while (!_stop)
                {
                    try
                    {
                        var groupEp = new IPEndPoint(IPAddress.Any,0);
                        var bytes = _udpCommandListener.Receive(ref groupEp);

                        //Logger.Info("Recevied Message from UDP COMMAND INTERFACE: "+ Encoding.UTF8.GetString(
                        //          bytes, 0, bytes.Length));
                        var message =
                            JsonConvert.DeserializeObject<UDPInterfaceCommand>(Encoding.UTF8.GetString(
                                bytes, 0, bytes.Length));

                        if (message?.Command == UDPInterfaceCommand.UDPCommandType.FREQUENCY_DELTA)
                        {
                            RadioHelper.UpdateRadioFrequency(message.Frequency, message.RadioId);
                        }
                        else if (message?.Command == UDPInterfaceCommand.UDPCommandType.FREQUENCY_SET)
                        {
                            RadioHelper.UpdateRadioFrequency(message.Frequency, message.RadioId,false,true);
                        }
                        else if (message?.Command == UDPInterfaceCommand.UDPCommandType.ACTIVE_RADIO)
                        {
                            RadioHelper.SelectRadio(message.RadioId);
                        }
                        else if (message?.Command == UDPInterfaceCommand.UDPCommandType.CHANNEL_UP)
                        {
                            RadioHelper.RadioChannelUp(message.RadioId);
                        }
                        else if (message?.Command == UDPInterfaceCommand.UDPCommandType.CHANNEL_DOWN)
                        {
                            RadioHelper.RadioChannelDown(message.RadioId);
                        }
                        else if (message?.Command == UDPInterfaceCommand.UDPCommandType.SET_VOLUME)
                        {
                            RadioHelper.SetRadioVolume(message.Volume, message.RadioId);
                        }
                        else
                        {
                            Logger.Error("Unknown UDP Command!");
                        }
                    }
                    catch (SocketException e)
                    {
                        // SocketException is raised when closing app/disconnecting, ignore so we don't log "irrelevant" exceptions
                        if (!_stop)
                        {
                            Logger.Error(e, "SocketException Handling IL2  Message");
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Exception Handling IL2  Message");
                    }
                }

                try
                {
                    _udpCommandListener.Close();
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Exception stoping IL2 listener ");
                }
                
            });
        }

        public void Stop()
        {
            _stop = true;

            try
            {
                _udpCommandListener?.Close();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
