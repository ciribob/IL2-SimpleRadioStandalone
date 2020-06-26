using System;
using System.Collections.Concurrent;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Newtonsoft.Json;
using NLog.Layouts;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common
{
    public class RadioReceivingState
    {
        [JsonIgnore]
        public long LastReceviedAt { get; set; }

        public bool IsSecondary { get; set; }
        public bool IsSimultaneous { get; set; }
        public int ReceivedOn { get; set; }

        public bool PlayedEndOfTransmission { get; set; }

        public string SentBy { get; set; }

        public bool IsReceiving
        {
            get
            {
                return (DateTime.Now.Ticks - LastReceviedAt) < 3500000;
            }
        }
    }
}