using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings.RadioChannels
{
    public class FilePresetChannelsStore : IPresetChannelsStore
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public IEnumerable<PresetChannel> LoadFromStore(string radioName)
        {
            var file = FindRadioFile(NormaliseString(radioName));

            if (file != null)
            {
                return ReadFrequenciesFromFile(file);
            }

            return new List<PresetChannel>();
        }

        private List<PresetChannel> ReadFrequenciesFromFile(string filePath)
        {
            List<PresetChannel> channels = new List<PresetChannel>();
            string[] lines = File.ReadAllLines(filePath);

            const double MHz = 1000000;
            if (lines?.Length > 0)
            {
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length > 0)
                    {
                        try
                        {
                            double frequency = Double.Parse(trimmed, CultureInfo.InvariantCulture);
                            channels.Add(new PresetChannel()
                            {
                                Text = trimmed,
                                Value = frequency * MHz,
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.Log(LogLevel.Info, "Error parsing frequency  ");
                        }
                    }
                }
            }

            return channels;
        }

        private string FindRadioFile(string radioName)
        {
            var files = Directory.GetFiles(Environment.CurrentDirectory);

            foreach (var fileAndPath in files)
            {
                var name = Path.GetFileNameWithoutExtension(fileAndPath);

                if (NormaliseString(name) == radioName)
                {
                    return fileAndPath;
                }
            }
            return null;
        }

        private string NormaliseString(string str)
        {
            //only allow alphanumeric, remove all spaces etc
            return Regex.Replace(str, "[^a-zA-Z0-9]", "").ToLower();
        }
    }
}