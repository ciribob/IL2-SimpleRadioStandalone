using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models
{
    public class SRSAddressMessage : IL2UDPMessage
    {
        private string SRSAddress { get; set; }

        public SRSAddressMessage(byte[] message, int offset,int length)
        {
            int strLen = message[offset];

            SRSAddress = System.Text.Encoding.ASCII.GetString(message, offset + 1, strLen);
        }
    }
}
