using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.ExternalAudio
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 6)
            {
                Console.WriteLine("Error incorrect parameters - should be path frequency modulation coalition port name");
                Console.WriteLine("Example: \"C:\\FULL\\PATH\\TO\\File.mp3\" 251.0 AM 1 5002 ciribob-robot");
            }
            else
            {
                string mp3 = args[0].Trim();
                double freq = double.Parse(args[1].Trim(), CultureInfo.InvariantCulture);
                string modulation = args[2].Trim().ToUpperInvariant();
                int coalition = int.Parse(args[3].Trim());
                int port = int.Parse(args[4].Trim());
                string name = args[5].Trim();

                ExternalAudioClient client = new ExternalAudioClient(mp3,freq,modulation,coalition,port,name);
                client.Start();

            }
        }
    }
}
