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
        public int ParentVehicleClientID { get; set; }
        public int ServerClientID { get; set; }
        public short Coalition { get; set; }

        public ClientDataMessage(byte[] message, int offset)
        {
            ClientID = BitConverter.ToInt32(message, offset);
            ParentVehicleClientID = BitConverter.ToInt32(message, offset+ 4);
            ServerClientID = BitConverter.ToInt32(message, offset + 4 +4);
            Coalition = BitConverter.ToInt16(message, offset+ 4+4+4);
        }

        public override string ToString()
        {
            return $"{this.GetType()} : ClientID {ClientID}  : ParentVehicleClientID {ParentVehicleClientID} : ServerClientID {ServerClientID} : Coalition {Coalition} ";
        }
    }
}
