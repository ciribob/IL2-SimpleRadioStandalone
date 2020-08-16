using System;
using System.Collections.Generic;
using System.Globalization;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.ExternalAudio
{
    public class Program
    {
  
        private static void ConfigureLogging()
        {
            var config = new NLog.Config.LoggingConfiguration();
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            logconsole.Layout = "${longdate}|${level:uppercase=true}|${message}";
            
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);

            // Apply config           
            NLog.LogManager.Configuration = config;
        }
        public static void Main(string[] args)
        {
            if (args.Length != 7)
            {
                Console.WriteLine("Error incorrect parameters - should be path or text frequency modulation coalition port name volume");
                Console.WriteLine("Example: \"C:\\FULL\\PATH\\TO\\File.mp3\" 251.0 AM 1 5002 ciribob-robot 0.5");
                Console.WriteLine("Example: \"I want this read out over this frequency - hello world! \" 251.0 AM 1 5002 ciribob-robot 0.5");
                Console.WriteLine("Example: \"I want this read out over TWO frequencies - hello world! \" 251.0,252.0 AM,AM 1 5002 ciribob-robot 0.5");
                Console.WriteLine("Path or Text - either a full path ending with .mp3 or text to read out");
                Console.WriteLine("Frequency in MHz comma separated - 251.0,252.0 or just 252.0  ");
                Console.WriteLine("Modulation AM or FM comma separated - AM,FM or just AM  ");
                Console.WriteLine("Coalition - 0 is Spectator, 1 is Red, 2 is Blue");
                Console.WriteLine("Port - 6002 is the default");
                Console.WriteLine("Name - name of your transmitter - no spaces");
                Console.WriteLine("Volume - 1.0 is max, 0.0 is silence");
            }
            else
            {
                ConfigureLogging();

                string mp3 = args[0].Trim();
                string freqs = args[1].Trim();
                string modulations = args[2].Trim().ToUpperInvariant();
                int coalition = int.Parse(args[3].Trim());
                int port = int.Parse(args[4].Trim());
                string name = args[5].Trim();
                float volume = float.Parse(args[6].Trim(), CultureInfo.InvariantCulture);

                //process freqs
                var freqStr= freqs.Split(',');

                List<double> freqDouble = new List<double>();
                foreach (var s in freqStr)
                {
                    freqDouble.Add(double.Parse(s, CultureInfo.InvariantCulture) * 1000000d);
                }

                var modulationStr = modulations.Split(',');

                List<RadioInformation.Modulation> modulation = new List<RadioInformation.Modulation>();
                foreach (var s in modulationStr)
                {
                    RadioInformation.Modulation mod;
                    if (RadioInformation.Modulation.TryParse(s.Trim().ToUpper(), out mod))
                    {
                        modulation.Add(mod);
                    }
                }

                if (modulation.Count != freqDouble.Count)
                {
                    Console.WriteLine($"Number of frequencies ({freqDouble.Count}) does not match number of modulations ({modulation.Count}) - They must match!" +
                                      $"\n\nFor example: 251.0,252.0 AM,AM ");
                    Console.WriteLine("QUITTING!");
                }
                else
                {

                    ExternalAudioClient client = new ExternalAudioClient(mp3, freqDouble.ToArray(), modulation.ToArray(), coalition, port, name, volume);
                    client.Start();
                }



            }
        }
    }
}
