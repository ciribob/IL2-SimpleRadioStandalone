using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models
{
   
    public abstract class IL2UDPMessage
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public enum MessageType
        {
            SRV_ADDR = 10,
            SRV_TITLE = 11,
            SRS_ADDRESS = 12,
            CLIENT_DATA = 13,
            CTRL_DATA = 14,
        }

        public static List<IL2UDPMessage> Process(byte[] message)
        {
            Stream stream = new MemoryStream(message);

            stream.Seek(10, SeekOrigin.Current);

            //number of indicator structs
            int indicatorCount = stream.ReadByte();

            for (int i = 0; i < indicatorCount; i++)
            {
                stream.Seek(2, SeekOrigin.Current);

                //indicator count
                uint indicators =(uint) stream.ReadByte();

                stream.Seek(4*indicators, SeekOrigin.Current);
            }

            List<IL2UDPMessage> list = new List<IL2UDPMessage>();

            //skip to event offset
            int eventCount = stream.ReadByte();
           
            for (int i =0; i < eventCount; i++)
            {
                byte part1 = (byte) stream.ReadByte();
                byte part2 = (byte) stream.ReadByte();

                int msgTypeInt = BitConverter.ToUInt16(new[] {part1, part2}, 0);

                uint eventSize = (uint) stream.ReadByte();
                
                try
                {
                    MessageType msgType = (MessageType)msgTypeInt;

                    // Type float corresponds to float IEEE 754 floating point type;
                    // Type DWORD corresponds to LSB unsigned integer(4 bytes)
                    // Type WORD corresponds to LSB unsigned short integer(2 bytes)
                    // Type BYTE corresponds to LSB unsigned char value(1 byte)
                    // Type STRING consists of sequence: String Length(1 byte), following string ASCII data
                    switch (msgType)
                    {
                        case MessageType.SRV_ADDR:
                            list.Add(new ServerAddressMessage(message, (int)stream.Position,(int)eventSize));
                            break;
                        case MessageType.SRV_TITLE:
                            list.Add(new ServerTitleMessage(message, (int)stream.Position, (int)eventSize));
                            break;
                        case MessageType.SRS_ADDRESS:
                            list.Add(new SRSAddressMessage(message, (int)stream.Position, (int)eventSize));
                            break;
                        case MessageType.CLIENT_DATA:
                            list.Add(new ClientDataMessage(message, (int)stream.Position));
                            break;
                        case MessageType.CTRL_DATA:
                            list.Add(new ControlDataMessage(message,(int)stream.Position));
                            break;
                        default:
                            break;

                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,"Error processing IL2 Data");
                }

                stream.Seek(eventSize, SeekOrigin.Current);

            }

            stream.Close();

            return list;
        }


    }
}
