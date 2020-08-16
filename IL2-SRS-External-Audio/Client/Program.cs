using System;
using System.Collections.Generic;
using System.Globalization;
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
            }
            else
            {
                ConfigureLogging();

                string mp3 = args[0].Trim();
                double freq = double.Parse(args[1].Trim(), CultureInfo.InvariantCulture);
                string modulation = args[2].Trim().ToUpperInvariant();
                int coalition = int.Parse(args[3].Trim());
                int port = int.Parse(args[4].Trim());
                string name = args[5].Trim();
                float volume = float.Parse(args[6].Trim(), CultureInfo.InvariantCulture);

                ExternalAudioClient client = new ExternalAudioClient(mp3, freq, modulation, coalition, port, name,volume);
                client.Start();

            }
        }
    }
}
