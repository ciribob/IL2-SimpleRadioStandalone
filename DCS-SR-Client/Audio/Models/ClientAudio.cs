using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client
{
    public class ClientAudio
    {
        public byte[] EncodedAudio { get; set; }
        public short[] PcmAudioShort { get; set; }
        public string ClientGuid { get; set; }
        public long ReceiveTime { get; set; }
        public int ReceivedRadio { get; set; }
        public double Frequency { get; internal set; }
        public short Modulation { get; internal set; }
        public float Volume { get; internal set; }
        public uint UnitId { get; set; }
        public short Encryption { get; internal set; }
        public bool Decryptable { get; internal set; }
        public RadioReceivingState RadioReceivingState { get; set; }
        public double RecevingPower { get; set; }
        public float LineOfSightLoss { get; set; }
        public ulong PacketNumber { get; set; }
        public string OriginalClientGuid { get; internal set; }
    }
}