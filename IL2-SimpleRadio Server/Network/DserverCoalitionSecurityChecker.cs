using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using NLog;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Network
{
    /// <summary>
	/// Check a client list against a running dserver, to ensure no-one is cheating
    /// by tuning into a coalition they're not allowed to. This requires both rcon
    /// access and logfile access.
    /// 
    /// If a user is flying as Axis, they can be Axis or Neutral.
    /// If a user is flying as Allied, they can be Allied or Neutral.
    /// </summary>
    public class DserverCoalitionSecurityChecker
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private Socket socket;
        private string socketAddress = string.Empty;
        private string rconUser = string.Empty;
        private string rconPassword = string.Empty;

        public void PerformCheck(ICollection<SRClient> currentClients)
        {
            if (EnsureConnected())
            {
                // Do the check
                string playerListResponse = SendCommand("getplayerlist");
                if (GetResponseCode(playerListResponse) != 1)
                {
                    Logger.Warn("Could not retrieve current player list.");
                    return;
                }
                List<PlayerInfo> players = DecodePlayerList(playerListResponse);
                // TODO: Also spin up an IL2LogMonitor to determine what coalition players are spawned as.
                //       For every player on SRS who is *not* in the neutral channel, check
                //       - are they on the dserver player list? if not, mute / deafen them on SRS
                //       - did they most recently spawn in a plane matching their SRS coalition? if not, mute / deafen them on SRS
            }
            else
            {
                Logger.Warn("Not connected to dserver, skipping coalition security check.");
            }
        }

        private bool EnsureConnected()
        {
            string desiredAddress = ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.DSERVER_RCON_ADDRESS).StringValue;
            string desiredUser = ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.DSERVER_RCON_USERNAME).StringValue;
            string desiredPassword = ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.DSERVER_RCON_PASSWORD).StringValue;

            if (socket == null)
            {
                return ConnectAndAuthorize(desiredAddress, desiredUser, desiredPassword);
            }
            else if (!socketAddress.Equals(desiredAddress) || !rconUser.Equals(desiredUser) || !rconPassword.Equals(desiredPassword))
            {
                ShutdownSocket();
                return ConnectAndAuthorize(socketAddress, rconUser, rconPassword);
            }
            else
            {
                if (!EnsureAuthorized())
                {
                    ShutdownSocket();
                    return ConnectAndAuthorize(socketAddress, rconUser, rconPassword);
                }
                return true;
            }
        }

        private void ShutdownSocket()
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            socket = null;
        }

        private bool EnsureAuthorized()
        {
            return GetResponseCode(SendCommand("mystatus")) == 1;
        }

        private bool ConnectAndAuthorize(string address, string user, string password)
        {
            try
            {
                string[] addressAndPort = address.Split(':');
                if (addressAndPort.Length != 2)
                {
                    Logger.Error("Invalid host address and port: {0}", addressAndPort);
                    return false;
                }
                IPAddress iPAddress;
                int port;
                if (!IPAddress.TryParse(addressAndPort[0], out iPAddress))
                {
                    iPAddress = GetIpFromHost(addressAndPort[0]);
                }
                if (!int.TryParse(addressAndPort[1], out port))
                {
                    Logger.Error("Invalid port: {0}", address);
                    return false;
                }
                IPEndPoint endPoint = new IPEndPoint(iPAddress, port);

                Logger.Debug("Connecting to dserver: {0}", endPoint);
                socket = new Socket(iPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, true);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 500);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.Connect(endPoint);
                Logger.Debug("Connected ok");

                socketAddress = address;
                rconUser = user;
                rconPassword = password;

                string response = SendCommand(string.Format("AUTH {0} {1}", rconUser, rconPassword));
                if (GetResponseCode(response) != 1)
                {
                    Logger.Warn("Could not authorize with dserver.");
                    ShutdownSocket();
                    return false;
                }
                return true;
            }
            catch (FormatException ex)
            {
                Logger.Error("Could not parse host address and/or port '{0}': {1}", address, ex.Message);
                return false;
            }
            catch (SocketException ex)
            {
                Logger.Warn("Failed to connect to dserver {0}: {1}", address, ex.Message);
                return false;
            }
        }

        private static readonly Regex RE_STATUS = new Regex(@"STATUS=(?<status>\d+)");

        private int GetResponseCode(string response)
        {
            if (response == null || string.Empty.Equals(response))
            {
                return -1;
            }
            Match m = RE_STATUS.Match(response);
            if (m.Success)
            {
                if (int.TryParse(m.Groups["status"].Value, out int status))
                {
                    return status;
                }
            }
            Logger.Warn("Could not determine response code from response: {0}", response);
            return -1;
        }

        private static readonly Regex RE_PLAYER_LIST = new Regex(@"playerList=(?<player_list>.+)$");
        private List<PlayerInfo> DecodePlayerList(string playerListResponse)
        {
            List<PlayerInfo> result = new List<PlayerInfo>();
            Match m = RE_PLAYER_LIST.Match(playerListResponse);
            if (m.Success)
            {
                string decoded = HttpUtility.UrlDecode(m.Groups["player_list"].Value);
                foreach (var csvInfo in decoded.Split('|'))
                {
                    string[] fields = csvInfo.Split(',');
                    if (fields.Length != 6)
                    {
                        Logger.Warn("Unable to parse playerList response fragment: {0}", csvInfo);
                        continue;
                    }
                    if ("cId".Equals(fields[0]))
                    {
                        // This is the header row
                        continue;
                    }
                    int cid = int.Parse(fields[0]);
                    int status = int.Parse(fields[1]);
                    int ping = int.Parse(fields[2]);
                    string name = HttpUtility.UrlDecode(fields[3]);
                    string playerIdGuid = HttpUtility.UrlDecode(fields[4]);
                    string profileId = HttpUtility.UrlDecode(fields[5]);
                    // TODO: Maybe we need things like client ID in future, for now just name and status should do
                    result.Add(new PlayerInfo(name, status));
                }
            }
            return result;
        }


        private string SendCommand(string command)
        {
            // Commands consist of an ASCII null-terminated string, preceded by a short
            // which tells the length of the following command.

            Logger.Debug("Sending command string: {0}", command);

            byte[] commandBytes = Encoding.ASCII.GetBytes(command);

            // Figure out the length including null terminator
            short length = Convert.ToInt16(commandBytes.Length + 1);
            byte[] lengthBytes = BitConverter.GetBytes(length);

            byte[] assembled = new byte[lengthBytes.Length + commandBytes.Length + 1];
            lengthBytes.CopyTo(assembled, 0);
            commandBytes.CopyTo(assembled, 2);
            assembled[assembled.Length - 1] = 0x00;

            socket.Send(assembled);

            byte[] receiveBuffer = new byte[16384];
            int bytesReceived = socket.Receive(receiveBuffer);
            Logger.Trace("Received {0} bytes from server", bytesReceived);

            // Response includes a null terminating byte, ignore that when converting to ASCII
            string response = Encoding.ASCII.GetString(receiveBuffer, 0, bytesReceived - 1);
            Logger.Trace("Decoded to ASCII: {0}", response);
            return response;
        }

        private IPAddress GetIpFromHost(string host)
        {
            var hosts = Dns.GetHostAddresses(host);
            if (hosts == null || hosts.Length == 0)
            {
                throw new FormatException(string.Format("Host not found: {0}", host));
            }
            return hosts[0];
        }

        private class PlayerInfo
        {
            public PlayerInfo(string name, int status)
            {
                Name = name;
                Status = status;
            }

            public string Name { get; }
            public int Status { get; }
        }
    }
}
