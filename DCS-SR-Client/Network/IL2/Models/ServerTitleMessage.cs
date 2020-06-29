using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models
{
    class ServerTitleMessage:IL2UDPMessage
    {
        private string ServerName { get; set; }

        public ServerTitleMessage(byte[] message, int offset,int length)
        {
            int strLen = message[offset];

            ServerName = System.Text.Encoding.ASCII.GetString(message, offset + 1, strLen);
        }

        public override string ToString()
        {
            return $"{this.GetType()} : {ServerName} ";
        }
    }
}
