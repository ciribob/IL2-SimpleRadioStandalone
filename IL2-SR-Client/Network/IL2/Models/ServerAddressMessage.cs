using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models
{
    public class ServerAddressMessage:IL2UDPMessage
    {
        public ServerAddressMessage(byte[] message, int offset, int length)
        {

            int strLen = message[offset];

            IL2ServerAddress = System.Text.Encoding.ASCII.GetString(message, offset+1, strLen);
        }

        private string IL2ServerAddress { get; set; }

        public override string ToString()
        {
            return $"{this.GetType()} : {IL2ServerAddress} ";
        }
    }
}
