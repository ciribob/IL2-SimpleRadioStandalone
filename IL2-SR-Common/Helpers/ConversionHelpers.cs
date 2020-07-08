using System;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common
{
    public class ConversionHelpers
    {
        public static short[] ByteArrayToShortArray(byte[] data)
        {
            var shortArry = new short[data.Length / sizeof(short)];
            Buffer.BlockCopy(data, 0, shortArry, 0, data.Length);
            return shortArry;
        }

        public static byte[] ShortArrayToByteArray(short[] shortArray)
        {
            var byteArray = new byte[shortArray.Length * sizeof(short)];
            Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);
            return byteArray;
        }

        public static short ToShort(byte byte1, byte byte2)
        {
            return (short) ((byte2 << 8) | byte1);
        }

        public static void FromShort(short number, out byte byte1, out byte byte2)
        {
            byte1 = (byte) (number & 0xFF);
            byte2 = (byte) (number >> 8);
        }
    }
}