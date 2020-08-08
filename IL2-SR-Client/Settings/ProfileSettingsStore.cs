using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Microsoft.Win32;
using NLog;
using SharpConfig;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings
{
    public enum ProfileSettingsKeys
    {
        Radio1Channel,
        Radio2Channel,
        Radio3Channel,
        Radio4Channel,
        Radio5Channel,
        Radio6Channel,
        Radio7Channel,
        Radio8Channel,
        Radio9Channel,
        Radio10Channel,
        IntercomChannel,

        RadioEffects,
        RadioEncryptionEffects, //Radio Encryption effects
        RadioEffectsClipping,
        NATOTone,

        RadioRxEffects_Start, // Recieving Radio Effects
        RadioRxEffects_End,
        RadioTxEffects_Start, // Recieving Radio Effects
        RadioTxEffects_End,

        AutoSelectPresetChannel, //auto select preset channel

        AlwaysAllowHotasControls,
        AllowIL2PTT,
        RadioSwitchIsPTT,


        AlwaysAllowTransponderOverlay,
        RadioSwitchIsPTTOnlyWhenValid,

        MIDSRadioEffect, //if on and Radio TX effects are on the MIDS tone is used

        WrapNextRadio, // if false radio up at 5 will stop

        EnableTextToSpeech, // if true - all changes are read out to channels or radios
        TextToSpeechVolume,
        PTTReleaseDelay, // How long to hold the PTT after release
    }

    public class ProfileSettingsStore
    {
        private static readonly object _lock = new object();
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        public string CurrentProfileName { get; set; } = "default";
        public string Path { get; }

        private readonly Dictionary<string, string> defaultSettingsProfileSettings = new Dictionary<string, string>()
        {
            {ProfileSettingsKeys.RadioEffects.ToString(), "true"},
            {ProfileSettingsKeys.RadioEffectsClipping.ToString(), "true"},
            {ProfileSettingsKeys.RadioEncryptionEffects.ToString(), "true"},
            {ProfileSettingsKeys.NATOTone.ToString(), "false"},

            {ProfileSettingsKeys.RadioRxEffects_Start.ToString(), "true"},
            {ProfileSettingsKeys.RadioRxEffects_End.ToString(), "true"},
            {ProfileSettingsKeys.RadioTxEffects_Start.ToString(), "true"},
            {ProfileSettingsKeys.RadioTxEffects_End.ToString(), "true"},
            {ProfileSettingsKeys.MIDSRadioEffect.ToString(), "true"},

            {ProfileSettingsKeys.AutoSelectPresetChannel.ToString(), "true"},

            {ProfileSettingsKeys.AlwaysAllowHotasControls.ToString(),"false" },
            {ProfileSettingsKeys.AllowIL2PTT.ToString(),"true" },
            {ProfileSettingsKeys.RadioSwitchIsPTT.ToString(), "false"},
            {ProfileSettingsKeys.RadioSwitchIsPTTOnlyWhenValid.ToString(), "false"},
            {ProfileSettingsKeys.AlwaysAllowTransponderOverlay.ToString(), "false"},
            {ProfileSettingsKeys.WrapNextRadio.ToString(), "true"},
            {ProfileSettingsKeys.EnableTextToSpeech.ToString(), "false"},
            {ProfileSettingsKeys.TextToSpeechVolume.ToString(), "1.0"},

            {ProfileSettingsKeys.PTTReleaseDelay.ToString(), "0"},
        };


        public List<string> ProfileNames
        {
            get
            {
                return new List<string>(InputProfiles.Keys);
            }
            
        }

        public Dictionary<InputBinding, InputDevice> GetCurrentInputProfile()
        {
            return InputProfiles[GetProfileName(CurrentProfileName)];
        }

        public Configuration GetCurrentProfile()
        {
            return InputConfigs[GetProfileCfgFileName(CurrentProfileName)];
        }
        public Dictionary<string, Dictionary<InputBinding, InputDevice>> InputProfiles { get; set; } = new Dictionary<string, Dictionary<InputBinding, InputDevice>>();

        private Dictionary<string, Configuration> InputConfigs = new Dictionary<string, Configuration>();

        private readonly GlobalSettingsStore _globalSettings;

        public ProfileSettingsStore(GlobalSettingsStore globalSettingsStore)
        {
            this._globalSettings = globalSettingsStore;
            this.Path = _globalSettings.Path;

            MigrateOldSettings();

            var profiles = GetProfiles();
            foreach (var profile in profiles)
            {
                Configuration _configuration = null;
                try
                {
                     _configuration = Configuration.LoadFromFile(Path+GetProfileCfgFileName(profile));
                    InputConfigs[GetProfileCfgFileName(profile)] = _configuration;

                    var inputProfile = new Dictionary<InputBinding, InputDevice>();
                    InputProfiles[GetProfileName(profile)] = inputProfile;

                    foreach (InputBinding bind in Enum.GetValues(typeof(InputBinding)))
                    {
                        var device = GetControlSetting(bind, _configuration);

                        if (device != null)
                        {
                            inputProfile[bind] = device;
                        }
                    }

                    _configuration.SaveToFile(Path+GetProfileCfgFileName(profile), Encoding.UTF8);
                
                }
                catch (FileNotFoundException ex)
                {
                    Logger.Info(
                        $"Did not find input config file at path {profile}, initialising with default config");
                }
                catch (ParserException ex)
                {
                    Logger.Info(
                        $"Error with input config - creating a new default ");
                }

                if (_configuration == null)
                {
                    _configuration = new Configuration();
                    var inputProfile = new Dictionary<InputBinding, InputDevice>();
                    InputProfiles[GetProfileName(profile)] = inputProfile;
                    InputConfigs[GetProfileCfgFileName(profile)] = new Configuration();
                    _configuration.SaveToFile(Path+GetProfileCfgFileName(profile), Encoding.UTF8);

                }
            }

            //add default
            if (!InputProfiles.ContainsKey(GetProfileName("default")))
            {
                InputConfigs[GetProfileCfgFileName("default")] = new Configuration();

                var inputProfile = new Dictionary<InputBinding, InputDevice>();
                InputProfiles[GetProfileName("default")] = inputProfile;

                InputConfigs[GetProfileCfgFileName("default")].SaveToFile(GetProfileCfgFileName("default"));
            }
        }

        private void MigrateOldSettings()
        {
            try
            {
                //combine global.cfg and input-default.cfg
                if (File.Exists(Path+"input-default.cfg") && File.Exists(Path + "global.cfg") && !File.Exists("default.cfg"))
                {
                    //Copy the current GLOBAL settings - not all relevant but will be ignored
                    File.Copy(Path + "global.cfg", Path + "default.cfg");

                    var inputText = File.ReadAllText(Path + "input-default.cfg", Encoding.UTF8);

                    File.AppendAllText(Path + "default.cfg", inputText, Encoding.UTF8);

                    Logger.Info(
                        $"Migrated the previous input-default.cfg and global settings to the new profile");
                }
                else
                {
                    Logger.Info(
                        $"No need to migrate - migration complete");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex,"Error migrating input profiles");
            }
          
        }

        public List<string> GetProfiles()
        {
            var profiles = _globalSettings.GetClientSetting(GlobalSettingsKeys.SettingsProfiles).StringValueArray;

            if (profiles == null || profiles.Length == 0 || !profiles.Contains("default"))
            {
                profiles = new[] { "default" };
                _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles);
            }

            return new List<string>(profiles);
        }

        public void AddNewProfile(string profileName)
        {
            var profiles = InputProfiles.Keys.ToList();
            profiles.Add(profileName);

            _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

            InputConfigs[GetProfileCfgFileName(profileName)] = new Configuration();

            var inputProfile = new Dictionary<InputBinding, InputDevice>();
            InputProfiles[GetProfileName(profileName)] = inputProfile;
        }

        private string GetProfileCfgFileName(string prof)
        {
            if (prof.Contains(".cfg"))
            {
                return prof;
            }

            return  prof + ".cfg";
        }

        private string GetProfileName(string cfg)
        {
            if (cfg.Contains(".cfg"))
            {
                return cfg.Replace(".cfg","");
            }

            return cfg;
        }

        public InputDevice GetControlSetting(InputBinding key)
        {
            return GetControlSetting(key, GetCurrentProfile());
        }

        public InputDevice GetControlSetting(InputBinding key, Configuration configuration)
        {
            if (!configuration.Contains(key.ToString()))
            {
                return null;
            }

            try
            {
                var device = new InputDevice();
                device.DeviceName = configuration[key.ToString()]["name"].StringValue;

                device.Button = configuration[key.ToString()]["button"].IntValue;
                device.InstanceGuid =
                    Guid.Parse(configuration[key.ToString()]["guid"].RawValue);
                device.InputBind = key;

                device.ButtonValue = configuration[key.ToString()]["value"].IntValue;

                return device;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error reading input device saved settings ");
            }


            return null;
        }
        public void SetControlSetting(InputDevice device)
        {
            RemoveControlSetting(device.InputBind);

            var configuration = GetCurrentProfile();

            configuration.Add(new Section(device.InputBind.ToString()));

            //create the sections
            var section = configuration[device.InputBind.ToString()];

            section.Add(new Setting("name", device.DeviceName.Replace("\0", "")));
            section.Add(new Setting("button", device.Button));
            section.Add(new Setting("value", device.ButtonValue));
            section.Add(new Setting("guid", device.InstanceGuid.ToString()));

            var inputDevices = GetCurrentInputProfile();

            inputDevices[device.InputBind] = device;

            Save();
        }

        public void RemoveControlSetting(InputBinding binding)
        {
            var configuration = GetCurrentProfile();

            if (configuration.Contains(binding.ToString()))
            {
                configuration.Remove(binding.ToString());
            }

            var inputDevices = GetCurrentInputProfile();
            inputDevices.Remove(binding);

            Save();
        }

        public Setting GetClientSetting(ProfileSettingsKeys key)
        {
            return GetSetting("Client Settings", key.ToString());
        }

        private Setting GetSetting(string section, string setting)
        {
            var _configuration = GetCurrentProfile();

            if (!_configuration.Contains(section))
            {
                _configuration.Add(section);
            }

            if (!_configuration[section].Contains(setting))
            {
                if (defaultSettingsProfileSettings.ContainsKey(setting))
                {
                    //save
                    _configuration[section]
                        .Add(new Setting(setting, defaultSettingsProfileSettings[setting]));

                    Save();
                }
                else if (defaultSettingsProfileSettings.ContainsKey(setting))
                {
                    //save
                    _configuration[section]
                        .Add(new Setting(setting, defaultSettingsProfileSettings[setting]));

                    Save();
                }
                else
                {
                    _configuration[section]
                        .Add(new Setting(setting, ""));
                    Save();
                }
            }

            return _configuration[section][setting];
        }

        public void SetClientSetting(ProfileSettingsKeys key, bool value)
        {
            SetSetting("Client Settings", key.ToString(), value);
        }
        public void SetClientSetting(ProfileSettingsKeys key, string value)
        {
            SetSetting("Client Settings", key.ToString(), value);
        }

        private void SetSetting(string section, string key, object setting)
        {
            var _configuration = GetCurrentProfile();

            if (setting == null)
            {
                setting = "";
            }
            if (!_configuration.Contains(section))
            {
                _configuration.Add(section);
            }

            if (!_configuration[section].Contains(key))
            {
                _configuration[section].Add(new Setting(key, setting));
            }
            else
            {
                if (setting is bool)
                {
                    _configuration[section][key].BoolValue = (bool)setting;
                }
                else if (setting.GetType() == typeof(string))
                {
                    _configuration[section][key].StringValue = setting as string;
                }
                else if (setting is string[])
                {
                    _configuration[section][key].StringValueArray = setting as string[];
                }
                else
                {
                    Logger.Error("Unknown Setting Type - Not Saved ");
                }

            }

            Save();
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var configuration = GetCurrentProfile();
                    configuration.SaveToFile(Path+GetProfileCfgFileName(CurrentProfileName));
                }
                catch (Exception ex)
                {
                    Logger.Error("Unable to save settings!");
                }
            }
        }

        public void RemoveProfile(string profile)
        {
            InputConfigs.Remove(GetProfileCfgFileName(profile));
            InputProfiles.Remove(GetProfileName(profile));

            var profiles = InputProfiles.Keys.ToList();
            _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

            try
            {
                File.Delete(Path+GetProfileCfgFileName(profile));
            }
            catch
            { }

            CurrentProfileName = "default";
        }

        public void RenameProfile(string oldName,string newName)
        {
            InputConfigs[GetProfileCfgFileName(newName)] = InputConfigs[GetProfileCfgFileName(oldName)];
            InputProfiles[GetProfileName(newName)]= InputProfiles[GetProfileName(oldName)];

            InputConfigs.Remove(GetProfileCfgFileName(oldName));
            InputProfiles.Remove(GetProfileName(oldName));

            var profiles = InputProfiles.Keys.ToList();
            _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

            CurrentProfileName = "default";

            InputConfigs[GetProfileCfgFileName(newName)].SaveToFile(GetProfileCfgFileName(newName));

            try
            {
                File.Delete(Path+GetProfileCfgFileName(oldName));
            }
            catch
            { }
        }

        public void CopyProfile(string profileToCopy, string profileName)
        {
            var config = Configuration.LoadFromFile(Path+GetProfileCfgFileName(profileToCopy));
            InputConfigs[GetProfileCfgFileName(profileName)] = config;

            var inputProfile = new Dictionary<InputBinding, InputDevice>();
            InputProfiles[GetProfileName(profileName)] = inputProfile;

            foreach (InputBinding bind in Enum.GetValues(typeof(InputBinding)))
            {
                var device = GetControlSetting(bind, config);

                if (device != null)
                {
                    inputProfile[bind] = device;
                }
            }

            var profiles = InputProfiles.Keys.ToList();
            _globalSettings.SetClientSetting(GlobalSettingsKeys.SettingsProfiles, profiles.ToArray());

            CurrentProfileName = "default";

            InputConfigs[GetProfileCfgFileName(profileName)].SaveToFile(Path+GetProfileCfgFileName(profileName));

        }

        public bool GetClientSettingBool(ProfileSettingsKeys key)
        {
            var setting = GetSetting("Client Settings", key.ToString());
            if (setting.RawValue.Length == 0)
            {
                return false;
            }

            return setting.BoolValue;
        }

    }
}
