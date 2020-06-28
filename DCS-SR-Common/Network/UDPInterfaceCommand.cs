using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Network
{
    public class UDPInterfaceCommand
    {
        public enum UDPCommandType
        {
            FREQUENCY_DELTA = 0,
            ACTIVE_RADIO = 1,
            CHANNEL_UP = 3,
            CHANNEL_DOWN = 4,
            SET_VOLUME = 5,
            FREQUENCY_SET = 12,
        }

        public int RadioId { get; set; }
        public double Frequency { get; set; }
        public UDPCommandType Command { get; set; }
        public float Volume { get; set; }

        public bool Enabled { get; set; }

        public int Code { get; set; }
    }
}