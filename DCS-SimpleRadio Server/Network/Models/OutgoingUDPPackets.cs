using System.Collections.Generic;
using System.Net;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Network
{
    public class OutgoingUDPPackets
    {
        public List<IPEndPoint> OutgoingEndPoints { get; set; }
        public byte[] ReceivedPacket { get; set; }
    }
}