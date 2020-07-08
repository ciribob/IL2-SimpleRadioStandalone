using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models
{
    public class ControlDataMessage : IL2UDPMessage
    {
        public int ParentVehicleClientID { get; set; }
        public short Coalition { get; set; }

        /*
         * Controlled object data. Sent on object creation and parent
            ID changes
            struct STControlledData{
            long nParentClientID; //ClientID of parent vehicle (if has, else -1)
            short nCoalitionID;
            };
         */

        public ControlDataMessage(byte[] message, int offset)
        {
            ParentVehicleClientID = BitConverter.ToInt32(message, offset);
            Coalition = BitConverter.ToInt16(message, offset+ 4);
        }

        public override string ToString()
        {
            return $"{this.GetType()} ParentVehicleClientID {ParentVehicleClientID} : Coalition {Coalition} ";
        }
    }
}
