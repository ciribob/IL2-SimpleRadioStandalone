using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models
{
    public class ClientDataMessage : IL2UDPMessage
    {
        public int ClientID { get; set; }
        public int ServerClientID { get; set; }

        public string PlayerName { get; set; }

        public ClientDataMessage(byte[] message, int offset)
        {
            ClientID = BitConverter.ToInt32(message, offset);
            ServerClientID = BitConverter.ToInt32(message, offset + 4);

            string name = System.Text.Encoding.ASCII.GetString(message, offset+8, 32).Trim();

            int pos = name.IndexOf('\0');
            if (pos >= 0)
                name = name.Substring(0, pos);

            PlayerName = name;
        }

        public override string ToString()
        {
            return $"{this.GetType()} : ClientID {ClientID} : ServerClientID {ServerClientID} : PlayerName {PlayerName} ";
        }
    }
}
