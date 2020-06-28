using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Preferences;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientList;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Overlay;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Dmo;
using NAudio.Wave;
using NLog;
using WPFCustomMessageBox;
using InputBinding = Ciribob.IL2.SimpleRadio.Standalone.Client.Settings.InputBinding;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public delegate void ReceivedAutoConnect(string address, int port);

        public delegate void ToggleOverlayCallback(bool uiButton);

        private readonly AudioManager _audioManager;

        private readonly string _guid;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private AudioPreview _audioPreview;
        private SRSClientSyncHandler _client;
        private int _port = 5002;

        private Overlay.RadioOverlayWindow _radioOverlayWindow;

        private IPAddress _resolvedIp;
        private ServerSettingsWindow _serverSettingsWindow;

        private ClientListWindow _clientListWindow;

        //used to debounce toggle
        private long _toggleShowHide;

        private readonly DispatcherTimer _updateTimer;
        private ServerAddress _serverAddress;
        private readonly DelegateCommand _connectCommand;

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

        /// <remarks>Used in the XAML for DataBinding many things</remarks>
        public ClientStateSingleton ClientState { get; } = ClientStateSingleton.Instance;
        
        /// <remarks>Used in the XAML for DataBinding the connected client count</remarks>
        public ConnectedClientsSingleton Clients { get; } = ConnectedClientsSingleton.Instance;

        /// <remarks>Used in the XAML for DataBinding input audio related UI elements</remarks>
        public AudioInputSingleton AudioInput { get; } = AudioInputSingleton.Instance;

        /// <remarks>Used in the XAML for DataBinding output audio related UI elements</remarks>
        public AudioOutputSingleton AudioOutput { get; } = AudioOutputSingleton.Instance;

        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        public MainWindow()
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            InitializeComponent();

            // Initialize ToolTip controls
            ToolTips.Init();

            // Initialize images/icons
            Images.Init();

            // Initialise sounds
            Sounds.Init();

            DataContext = this;

            var client = ClientStateSingleton.Instance;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = _globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientX).DoubleValue;
            Top = _globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientY).DoubleValue;



            Title = Title + " - " + UpdaterChecker.VERSION;

            CheckWindowVisibility();

            if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.StartMinimised))
            {
                Hide();
                WindowState = WindowState.Minimized;

                Logger.Info("Started IL2-SimpleRadio Client " + UpdaterChecker.VERSION + " minimized");
            }
            else
            {
                Logger.Info("Started IL2-SimpleRadio Client " + UpdaterChecker.VERSION);
            }

            _guid = ClientStateSingleton.Instance.ShortGUID;
            Analytics.Log("Client", "Startup", _globalSettings.GetClientSetting(GlobalSettingsKeys.ClientIdLong).RawValue);

            InitSettingsScreen();

            InitSettingsProfiles();
            ReloadProfile();

            InitInput();

            _connectCommand = new DelegateCommand(Connect, () => ServerAddress != null);
            FavouriteServersViewModel = new FavouriteServersViewModel(new CsvFavouriteServerStore());

            InitDefaultAddress();

            SpeakerBoost.Value = _globalSettings.GetClientSetting(GlobalSettingsKeys.SpeakerBoost).DoubleValue;

            Speaker_VU.Value = -100;
            Mic_VU.Value = -100;

            _audioManager = new AudioManager(AudioOutput.WindowsN);
            _audioManager.SpeakerBoost = VolumeConversionHelper.ConvertVolumeSliderToScale((float) SpeakerBoost.Value);


            if ((SpeakerBoostLabel != null) && (SpeakerBoost != null))
            {
                SpeakerBoostLabel.Content = VolumeConversionHelper.ConvertLinearDiffToDB(_audioManager.SpeakerBoost);
            }

            UpdaterChecker.CheckForUpdate(_globalSettings.GetClientSettingBool(GlobalSettingsKeys.CheckForBetaUpdates));

            InitFlowDocument();

            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(100)};
            _updateTimer.Tick += UpdatePlayerLocationAndVUMeters;
            _updateTimer.Start();

        }

        private void CheckWindowVisibility()
        {
            if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.DisableWindowVisibilityCheck))
            {
                Logger.Info("Window visibility check is disabled, skipping");
                return;
            }

            bool mainWindowVisible = false;
            bool radioWindowVisible = false;
            bool awacsWindowVisible = false;

            int mainWindowX = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientX).DoubleValue;
            int mainWindowY = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.ClientY).DoubleValue;
            int radioWindowX = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioX).DoubleValue;
            int radioWindowY = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioY).DoubleValue;
            int awacsWindowX = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.AwacsX).DoubleValue;
            int awacsWindowY = (int)_globalSettings.GetPositionSetting(GlobalSettingsKeys.AwacsY).DoubleValue;

            Logger.Info($"Checking window visibility for main client window {{X={mainWindowX},Y={mainWindowY}}}");
            Logger.Info($"Checking window visibility for radio overlay {{X={radioWindowX},Y={radioWindowY}}}");
            Logger.Info($"Checking window visibility for AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}}");

            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                Logger.Info($"Checking {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds} for window visibility");

                if (screen.Bounds.Contains(mainWindowX, mainWindowY))
                {
                    Logger.Info($"Main client window {{X={mainWindowX},Y={mainWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    mainWindowVisible = true;
                }
                if (screen.Bounds.Contains(radioWindowX, radioWindowY))
                {
                    Logger.Info($"Radio overlay {{X={radioWindowX},Y={radioWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    radioWindowVisible = true;
                }
                if (screen.Bounds.Contains(awacsWindowX, awacsWindowY))
                {
                    Logger.Info($"AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    awacsWindowVisible = true;
                }
            }

            if (!mainWindowVisible)
            {
                MessageBox.Show(this,
                    "The SRS client window is no longer visible likely due to a monitor reconfiguration.\n\nThe position will be reset to default to fix this issue.",
                    "SRS window position reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Logger.Warn($"Main client window outside visible area of monitors, resetting position ({mainWindowX},{mainWindowY}) to defaults");

                _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientX, 200);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientY, 200);

                Left = 200;
                Top = 200;
            }

            if (!radioWindowVisible)
            {
                MessageBox.Show(this,
                    "The SRS radio overlay is no longer visible likely due to a monitor reconfiguration.\n\nThe position will be reset to default to fix this issue.",
                    "SRS window position reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Logger.Warn($"Radio overlay window outside visible area of monitors, resetting position ({radioWindowX},{radioWindowY}) to defaults");

                _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, 300);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY, 300);

                if (_radioOverlayWindow != null)
                {
                    _radioOverlayWindow.Left = 300;
                    _radioOverlayWindow.Top = 300;
                }
            }

            if (!awacsWindowVisible)
            {
                MessageBox.Show(this,
                    "The SRS AWACS overlay is no longer visible likely due to a monitor reconfiguration.\n\nThe position will be reset to default to fix this issue",
                    "SRS window position reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Logger.Warn($"AWACS overlay window outside visible area of monitors, resetting position ({awacsWindowX},{awacsWindowY}) to defaults");

                _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsX, 300);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsY, 300);
            }
        }

        private void InitFlowDocument()
        {
            //make hyperlinks work
            var hyperlinks = WPFElementHelper.GetVisuals(AboutFlowDocument).OfType<Hyperlink>();
            foreach (var link in hyperlinks)
                link.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler((sender, args) =>
                {
                    Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri));
                    args.Handled = true;
                });
        }

        private void InitDefaultAddress()
        {
            // legacy setting migration
            if (!string.IsNullOrEmpty(_globalSettings.GetClientSetting(GlobalSettingsKeys.LastServer).StringValue) &&
                FavouriteServersViewModel.Addresses.Count == 0)
            {
                var oldAddress = new ServerAddress(_globalSettings.GetClientSetting(GlobalSettingsKeys.LastServer).StringValue,
                    _globalSettings.GetClientSetting(GlobalSettingsKeys.LastServer).StringValue, true);
                FavouriteServersViewModel.Addresses.Add(oldAddress);
            }

            ServerAddress = FavouriteServersViewModel.DefaultServerAddress;
        }

        private void InitSettingsProfiles()
        {
            ControlsProfile.IsEnabled = false;
            ControlsProfile.Items.Clear();
            foreach (var profile in _globalSettings.ProfileSettingsStore.InputProfiles.Keys)
            {
                ControlsProfile.Items.Add(profile);
            } 
            ControlsProfile.IsEnabled = true;
            ControlsProfile.SelectedIndex = 0;

            CurrentProfile.Content = _globalSettings.ProfileSettingsStore.CurrentProfileName;

        }

        void ReloadProfile()
        {
            //switch profiles
            Logger.Info(ControlsProfile.SelectedValue as string + " - Profile now in use");
            _globalSettings.ProfileSettingsStore.CurrentProfileName = ControlsProfile.SelectedValue as string;

            //redraw UI
            ReloadInputBindings();
            ReloadProfileSettings();
            ReloadRadioAudioChannelSettings();

            CurrentProfile.Content = _globalSettings.ProfileSettingsStore.CurrentProfileName;
        }

        private void InitInput()
        {
            InputManager = new InputDeviceManager(this, ToggleOverlay);

            InitSettingsProfiles();

            ControlsProfile.SelectionChanged += OnProfileDropDownChanged;

            Radio1.InputName = "Radio 1";
            Radio1.ControlInputBinding = InputBinding.Switch1;
            Radio1.InputDeviceManager = InputManager;

            PTT.InputName = "Push To Talk - PTT";
            PTT.ControlInputBinding = InputBinding.Ptt;
            PTT.InputDeviceManager = InputManager;

            Intercom.InputName = "Intercom Select";
            Intercom.ControlInputBinding = InputBinding.Intercom;
            Intercom.InputDeviceManager = InputManager;

            RadioOverlay.InputName = "Overlay Toggle";
            RadioOverlay.ControlInputBinding = InputBinding.OverlayToggle;
            RadioOverlay.InputDeviceManager = InputManager;

            NextRadio.InputName = "Select Next Radio";
            NextRadio.ControlInputBinding = InputBinding.NextRadio;
            NextRadio.InputDeviceManager = InputManager;

            PreviousRadio.InputName = "Select Previous Radio";
            PreviousRadio.ControlInputBinding = InputBinding.PreviousRadio;
            PreviousRadio.InputDeviceManager = InputManager;

            RadioChannelUp.InputName = "Radio Channel Up";
            RadioChannelUp.ControlInputBinding = InputBinding.RadioChannelUp;
            RadioChannelUp.InputDeviceManager = InputManager;

            RadioChannelDown.InputName = "Radio Channel Down";
            RadioChannelDown.ControlInputBinding = InputBinding.Down0001;
            RadioChannelDown.InputDeviceManager = InputManager;
        }

        private void OnProfileDropDownChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ControlsProfile.IsEnabled)
                ReloadProfile();
        }

        private void ReloadInputBindings()
        {
            Radio1.LoadInputSettings();
            PTT.LoadInputSettings();
            Intercom.LoadInputSettings();
            RadioOverlay.LoadInputSettings();
            NextRadio.LoadInputSettings();
            PreviousRadio.LoadInputSettings();
            RadioChannelUp.LoadInputSettings();
            RadioChannelDown.LoadInputSettings();
        }

        private void ReloadRadioAudioChannelSettings()
        {
            Radio1Config.Reload();
            Radio2Config.Reload();
            Radio3Config.Reload();
            Radio4Config.Reload();
            Radio5Config.Reload();
            Radio6Config.Reload();
            Radio7Config.Reload();
            Radio8Config.Reload();
            Radio9Config.Reload();
            Radio10Config.Reload();
            IntercomConfig.Reload();
        }

       
        public InputDeviceManager InputManager { get; set; }

        public FavouriteServersViewModel FavouriteServersViewModel { get; }

        public ServerAddress ServerAddress
        {
            get { return _serverAddress; }
            set
            {
                _serverAddress = value;
                if (value != null)
                {
                    ServerIp.Text = value.Address;
                }

                _connectCommand.RaiseCanExecuteChanged();
            }
        }

        public ICommand ConnectCommand => _connectCommand;

        private void UpdatePlayerLocationAndVUMeters(object sender, EventArgs e)
        {
            if (_audioPreview != null)
            {
                // Only update mic volume output if an audio input device is available - sometimes the value can still change, leaving the user with the impression their mic is working after all
                if (AudioInput.MicrophoneAvailable)
                {
                    Mic_VU.Value = _audioPreview.MicMax;
                }
                Speaker_VU.Value = _audioPreview.SpeakerMax;
            }
            else if (_audioManager != null)
            {
                // Only update mic volume output if an audio input device is available - sometimes the value can still change, leaving the user with the impression their mic is working after all
                if (AudioInput.MicrophoneAvailable)
                {
                    Mic_VU.Value = _audioManager.MicMax;
                }
                Speaker_VU.Value = _audioManager.SpeakerMax;
            }
            else
            {
                Mic_VU.Value = -100;
                Speaker_VU.Value = -100;
            }

            ConnectedClientsSingleton.Instance.NotifyAll();

        }

        private void InitSettingsScreen()
        {
            AutoConnectPromptToggle.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt);
            AutoConnectMismatchPromptToggle.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectMismatchPrompt);
            RadioOverlayTaskbarItem.IsChecked =
                _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide);
            RefocusIL2.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RefocusIL2);
            ExpandInputDevices.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ExpandControls);

            MinimiseToTray.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.MinimiseToTray);
            StartMinimised.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.StartMinimised);

            MicAGC.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AGC);
            MicDenoise.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.Denoise);

            CheckForBetaUpdates.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.CheckForBetaUpdates);
            PlayConnectionSounds.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds);

            RequireAdminToggle.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RequireAdmin);

            ShowTransmitterName.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ShowTransmitterName);
        }

        private void ReloadProfileSettings()
        {
            RadioEncryptionEffectsToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEncryptionEffects);
            RadioSwitchIsPTT.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT);

            RadioTxStartToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start);
            RadioTxEndToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_End);

            RadioRxStartToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start);
            RadioRxEndToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End);

            RadioMIDSToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.MIDSRadioEffect);

            RadioSoundEffects.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
            RadioSoundEffectsClipping.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);
            NATORadioToneToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.NATOTone);

            AutoSelectChannel.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AutoSelectPresetChannel);

            AlwaysAllowHotas.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AlwaysAllowHotasControls);
            AllowIL2PTT.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AllowIL2PTT);
            AlwaysAllowTransponderOverlay.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.AlwaysAllowTransponderOverlay);
        }

        private void Connect()
        {
            if (ClientState.IsConnected)
            {
                Stop();
            }
            else
            {
                SaveSelectedInputAndOutput();

                try
                {
                    //process hostname
                    var resolvedAddresses = Dns.GetHostAddresses(GetAddressFromTextBox());
                    var ip = resolvedAddresses.FirstOrDefault(xa => xa.AddressFamily == AddressFamily.InterNetwork); // Ensure we get an IPv4 address in case the host resolves to both IPv6 and IPv4

                    if (ip != null)
                    {
                        _resolvedIp = ip;
                        _port = GetPortFromTextBox();

                        _client = new SRSClientSyncHandler(_guid, UpdateUICallback, delegate(string name)
                        {
                            try
                            {
                                //on MAIN thread
                                Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
                                    new ThreadStart(() =>
                                    {
                                        //Handle Aircraft Name - find matching profile and select if you can
                                        name = Regex.Replace(name.Trim().ToLower(), "[^a-zA-Z0-9]", "");

                                        foreach (var profileName in _globalSettings.ProfileSettingsStore.ProfileNames)
                                        {
                                            if (name.StartsWith(Regex.Replace(profileName.Trim().ToLower(), "[^a-zA-Z0-9]",
                                                "")))
                                            {
                                                ControlsProfile.SelectedItem = profileName;
                                                return;
                                            }
                                        }

                                        ControlsProfile.SelectedIndex = 0;

                                    }));
                            }
                            catch (Exception ex)
                            {
                            }

                        });
                        _client.TryConnect(new IPEndPoint(_resolvedIp, _port), ConnectCallback);

                        StartStop.Content = "Connecting...";
                        StartStop.IsEnabled = false;
                        Mic.IsEnabled = false;
                        Speakers.IsEnabled = false;
                        MicOutput.IsEnabled = false;
                        Preview.IsEnabled = false;

                        if (_audioPreview != null)
                        {
                            Preview.Content = "Audio Preview";
                            _audioPreview.StopEncoding();
                            _audioPreview = null;
                        }
                    }
                    else
                    {
                        //invalid ID
                        MessageBox.Show("Invalid IP or Host Name!", "Host Name Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        ClientState.IsConnected = false;
                        ToggleServerSettings.IsEnabled = false;
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
                {
                    MessageBox.Show("Invalid IP or Host Name!", "Host Name Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    ClientState.IsConnected = false;
                    ToggleServerSettings.IsEnabled = false;
                }
            }
        }

        private string GetAddressFromTextBox()
        {
            var addr = ServerIp.Text.Trim();

            if (addr.Contains(":"))
            {
                return addr.Split(':')[0];
            }

            return addr;
        }

        private int GetPortFromTextBox()
        {
            var addr = ServerIp.Text.Trim();

            if (addr.Contains(":"))
            {
                int port;
                if (int.TryParse(addr.Split(':')[1], out port))
                {
                    return port;
                }
                throw new ArgumentException("specified port is not valid");
            }

            return 5002;
        }

        private void Stop(bool connectionError = false)
        {
            if (ClientState.IsConnected && _globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds))
            {
                try
                {
                    Sounds.BeepDisconnected.Play();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to play disconnect sound");
                }
            }

            ClientState.IsConnectionErrored = connectionError;

            StartStop.Content = "Connect";
            StartStop.IsEnabled = true;
            Mic.IsEnabled = true;
            Speakers.IsEnabled = true;
            MicOutput.IsEnabled = true;
            Preview.IsEnabled = true;
            ClientState.IsConnected = false;
            ToggleServerSettings.IsEnabled = false;

            if (!string.IsNullOrWhiteSpace(ClientState.LastSeenName) &&
                _globalSettings.GetClientSetting(GlobalSettingsKeys.LastSeenName).StringValue != ClientState.LastSeenName)
            {
                _globalSettings.SetClientSetting(GlobalSettingsKeys.LastSeenName, ClientState.LastSeenName);
            }

            try
            {
                _audioManager.StopEncoding();
            }
            catch (Exception ex)
            {
            }

            if (_client != null)
            {
                _client.Disconnect();
                _client = null;
            }

            ClientState.PlayerRadioInfo.Reset();
        }

        private void SaveSelectedInputAndOutput()
        {
            //save app settings
            // Only save selected microphone if one is actually available, resulting in a crash otherwise
            if (AudioInput.MicrophoneAvailable)
            {
                if(AudioInput.SelectedAudioInput.Value == null)
                {
                    _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, "default");

                }
                else
                {
                    var input = ((MMDevice)AudioInput.SelectedAudioInput.Value).ID;
                    _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, input);
                }
            }

            if (AudioOutput.SelectedAudioOutput.Value == null)
            {
                _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioOutputDeviceId, "default");
            }
            else
            {
                var output = (MMDevice)AudioOutput.SelectedAudioOutput.Value;
                _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioOutputDeviceId, output.ID);
            }

            //check if we have optional output
            if (AudioOutput.SelectedMicAudioOutput.Value != null)
            {
                var micOutput = (MMDevice)AudioOutput.SelectedMicAudioOutput.Value;
                _globalSettings.SetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId, micOutput.ID);
            }
            else
            {
                _globalSettings.SetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId, "");
            }
        }

        private void ConnectCallback(bool result, bool connectionError, string connection)
        {
            string currentConnection = ServerIp.Text.Trim();
            if (!currentConnection.Contains(":"))
            {
                currentConnection += ":5002";
            }

            if (result)
            {
                if (!ClientState.IsConnected)
                {
                    try
                    {
                        StartStop.Content = "Disconnect";
                        StartStop.IsEnabled = true;

                        ClientState.IsConnected = true;
                        ClientState.IsVoipConnected = false;

                        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds))
                        {
                            try
                            {
                                Sounds.BeepConnected.Play();
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn(ex, "Failed to play connect sound");
                            }
                        }

                        _globalSettings.SetClientSetting(GlobalSettingsKeys.LastServer, ServerIp.Text);

                        _audioManager.StartEncoding(_guid, InputManager,
                            _resolvedIp, _port);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex,
                            "Unable to get audio device - likely output device error - Pick another. Error:" +
                            ex.Message);
                        Stop();

                        var messageBoxResult = CustomMessageBox.ShowYesNo(
                            "Problem initialising Audio Output!\n\nTry a different Output device and please post your clientlog.txt to the support Discord server.\n\nJoin support Discord server now?",
                            "Audio Output Error",
                            "OPEN PRIVACY SETTINGS",
                            "JOIN DISCORD SERVER",
                            MessageBoxImage.Error);

                        if (messageBoxResult == MessageBoxResult.Yes) Process.Start("https://discord.gg/baw7g3t");
                    }
                }
            }
            else if (string.Equals(currentConnection, connection, StringComparison.OrdinalIgnoreCase))
            {
                // Only stop connection/reset state if connection is currently active
                // Autoconnect mismatch will quickly disconnect/reconnect, leading to double-callbacks
                Stop(connectionError);
            }
            else
            {
                if (!ClientState.IsConnected)
                {
                    Stop(connectionError);
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientX, Left);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientY, Top);

            if (!string.IsNullOrWhiteSpace(ClientState.LastSeenName) &&
                _globalSettings.GetClientSetting(GlobalSettingsKeys.LastSeenName).StringValue != ClientState.LastSeenName)
            {
                _globalSettings.SetClientSetting(GlobalSettingsKeys.LastSeenName, ClientState.LastSeenName);
            }

            //save window position
            base.OnClosing(e);

            //stop timer
            _updateTimer?.Stop();

            Stop();

            _audioPreview?.StopEncoding();
            _audioPreview = null;

            _radioOverlayWindow?.Close();
            _radioOverlayWindow = null;


        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _globalSettings.GetClientSettingBool(GlobalSettingsKeys.MinimiseToTray))
            {
                Hide();
            }

            base.OnStateChanged(e);
        }

        private void PreviewAudio(object sender, RoutedEventArgs e)
        {
            if (_audioPreview == null)
            {
                if (!AudioInput.MicrophoneAvailable)
                {
                    Logger.Info("Unable to preview audio, no valid audio input device available or selected");
                    return;
                }

                //get device
                try
                {
                    SaveSelectedInputAndOutput();

                    _audioPreview = new AudioPreview();
                    _audioPreview.SpeakerBoost = VolumeConversionHelper.ConvertVolumeSliderToScale((float)SpeakerBoost.Value);
                    _audioPreview.StartPreview(AudioOutput.WindowsN);

                    Preview.Content = "Stop Preview";
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,
                        "Unable to preview audio - likely output device error - Pick another. Error:" + ex.Message);

                }
            }
            else
            {
                Preview.Content = "Audio Preview";
                _audioPreview.StopEncoding();
                _audioPreview = null;
            }
        }

        private void UpdateUICallback()
        {
            if (ClientState.IsConnected)
            {
                ToggleServerSettings.IsEnabled = true;
            }
            else
            {
                ToggleServerSettings.IsEnabled = false;
            }
        }

        private void SpeakerBoost_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var convertedValue = VolumeConversionHelper.ConvertVolumeSliderToScale((float) SpeakerBoost.Value);

            if (_audioPreview != null)
            {
                _audioPreview.SpeakerBoost = convertedValue;
            }
            if (_audioManager != null)
            {
                _audioManager.SpeakerBoost = convertedValue;
            }

            _globalSettings.SetClientSetting(GlobalSettingsKeys.SpeakerBoost,
                SpeakerBoost.Value.ToString(CultureInfo.InvariantCulture));


            if ((SpeakerBoostLabel != null) && (SpeakerBoost != null))
            {
                SpeakerBoostLabel.Content = VolumeConversionHelper.ConvertLinearDiffToDB(convertedValue);
            }
        }

        private void RadioEncryptionEffects_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioEncryptionEffects,
                (string) RadioEncryptionEffectsToggle.Content);
        }

        private void NATORadioTone_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.NATOTone,
                (string)NATORadioToneToggle.Content);
        }

        private void RadioSwitchPTT_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioSwitchIsPTT, (string) RadioSwitchIsPTT.Content);
        }

        private void ShowOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleOverlay(true);
        }

        private void ToggleOverlay(bool uiButton)
        {
            //debounce show hide (1 tick = 100ns, 6000000 ticks = 600ms debounce)
            if ((DateTime.Now.Ticks - _toggleShowHide > 6000000) || uiButton)
            {
                _toggleShowHide = DateTime.Now.Ticks;
                if ((_radioOverlayWindow == null) || !_radioOverlayWindow.IsVisible ||
                    (_radioOverlayWindow.WindowState == WindowState.Minimized))
                {

                    _radioOverlayWindow?.Close();

                    _radioOverlayWindow = new Overlay.RadioOverlayWindow();


                    _radioOverlayWindow.ShowInTaskbar =
                        !_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide);
                    _radioOverlayWindow.Show();
                }
                else
                {
                    _radioOverlayWindow?.Close();
                    _radioOverlayWindow = null;
                }
            }
        }


        private void AutoConnect(string address, int port)
        {
            string connection = $"{address}:{port}";

            Logger.Info($"Received AutoConnect IL2-SRS @ {connection}");

            if (ClientState.IsConnected)
            {
                // Always show prompt about active/advertised SRS connection mismatch if client is already connected
                string[] currentConnectionParts = ServerIp.Text.Trim().Split(':');
                string currentAddress = currentConnectionParts[0];
                int currentPort = 5002;
                if (currentConnectionParts.Length >= 2)
                {
                    if (!int.TryParse(currentConnectionParts[1], out currentPort))
                    {
                        Logger.Warn($"Failed to parse port {currentConnectionParts[1]} of current connection, falling back to 5002 for autoconnect comparison");
                        currentPort = 5002;
                    }
                }
                string currentConnection = $"{currentAddress}:{currentPort}";

                if (string.Equals(address, currentAddress, StringComparison.OrdinalIgnoreCase) && port == currentPort)
                {
                    // Current connection matches SRS server advertised by IL2, all good
                    Logger.Info($"Current SRS connection {currentConnection} matches advertised server {connection}, ignoring autoconnect");
                    return;
                }
                else if (port != currentPort)
                {
                    // Port mismatch, will always be a different server, no need to perform hostname lookups
                    HandleAutoConnectMismatch(currentConnection, connection);
                    return;
                }

                // Perform DNS lookup of advertised and current hostnames to find hostname/resolved IP matches
                List<string> currentIPs = new List<string>();

                if (IPAddress.TryParse(currentAddress, out IPAddress currentIP))
                {
                    currentIPs.Add(currentIP.ToString());
                }
                else
                {
                    try
                    {
                        foreach (IPAddress ip in Dns.GetHostAddresses(currentConnectionParts[0]))
                        {
                            // SRS currently only supports IPv4 (due to address/port parsing)
                            if (ip.AddressFamily == AddressFamily.InterNetwork)
                            {
                                currentIPs.Add(ip.ToString());
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(e, $"Failed to resolve current SRS host {currentConnectionParts[0]} to IP addresses, ignoring autoconnect advertisement");
                        return;
                    }
                }

                List<string> advertisedIPs = new List<string>();

                if (IPAddress.TryParse(address, out IPAddress advertisedIP))
                {
                    advertisedIPs.Add(advertisedIP.ToString());
                }
                else
                {
                    try
                    {
                        foreach (IPAddress ip in Dns.GetHostAddresses(connection))
                        {
                            // SRS currently only supports IPv4 (due to address/port parsing)
                            if (ip.AddressFamily == AddressFamily.InterNetwork)
                            {
                                advertisedIPs.Add(ip.ToString());
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Warn(e, $"Failed to resolve advertised SRS host {address} to IP addresses, ignoring autoconnect advertisement");
                        return;
                    }
                }

                if (!currentIPs.Intersect(advertisedIPs).Any())
                {
                    // No resolved IPs match, display mismatch warning
                    HandleAutoConnectMismatch(currentConnection, connection);
                }
            }
            else
            {
                // Show auto connect prompt if client is not connected yet and setting has been enabled, otherwise automatically connect
                bool showPrompt = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt);

                bool connectToServer = !showPrompt;
                if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt))
                {
                    WindowHelper.BringProcessToFront(Process.GetCurrentProcess());

                    var result = MessageBox.Show(this,
                        $"Would you like to try to auto-connect to IL2-SRS @ {address}:{port}? ", "Auto Connect",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    connectToServer = (result == MessageBoxResult.Yes) && (StartStop.Content.ToString().ToLower() == "connect");
                }

                if (connectToServer)
                {
                    ServerIp.Text = connection;
                    Connect();
                }
            }
        }

        private async void HandleAutoConnectMismatch(string currentConnection, string advertisedConnection)
        {
            // Show auto connect mismatch prompt if setting has been enabled (default), otherwise automatically switch server
            bool showPrompt = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectMismatchPrompt);

            Logger.Info($"Current SRS connection {currentConnection} does not match advertised server {advertisedConnection}, {(showPrompt ? "displaying mismatch prompt" : "automatically switching server")}");

            bool switchServer = !showPrompt;
            if (showPrompt)
            {
                WindowHelper.BringProcessToFront(Process.GetCurrentProcess());

                var result = MessageBox.Show(this,
                    $"The SRS server advertised by IL2 @ {advertisedConnection} does not match the SRS server @ {currentConnection} you are currently connected to.\n\n" +
                    $"Would you like to connect to the advertised SRS server?",
                    "Auto Connect Mismatch",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                switchServer = result == MessageBoxResult.Yes;
            }

            if (switchServer)
            {
                Stop();

                StartStop.IsEnabled = false;
                StartStop.Content = "Connecting...";
                await Task.Delay(2000);
                StartStop.IsEnabled = true;
                ServerIp.Text = advertisedConnection;
                Connect();
            }
        }

        private void ResetRadioWindow_Click(object sender, RoutedEventArgs e)
        {
            //close overlay
            _radioOverlayWindow?.Close();
            _radioOverlayWindow = null;

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY,300);
                            
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioWidth, 122);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioHeight, 270);
                         
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioOpacity, 1.0);
        }

        private void ToggleServerSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if ((_serverSettingsWindow == null) || !_serverSettingsWindow.IsVisible ||
                (_serverSettingsWindow.WindowState == WindowState.Minimized))
            {
                _serverSettingsWindow?.Close();

                _serverSettingsWindow = new ServerSettingsWindow();
                _serverSettingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _serverSettingsWindow.Owner = this;
                _serverSettingsWindow.Show();
            }
            else
            {
                _serverSettingsWindow?.Close();
                _serverSettingsWindow = null;
            }
        }

        private void AutoConnectPromptToggle_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoConnectPrompt,(bool) AutoConnectPromptToggle.IsChecked);
        }

        private void AutoConnectMismatchPromptToggle_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoConnectMismatchPrompt, (bool) AutoConnectMismatchPromptToggle.IsChecked);
        }

        private void RadioOverlayTaskbarItem_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RadioOverlayTaskbarHide, (bool) RadioOverlayTaskbarItem.IsChecked);

            if (_radioOverlayWindow != null)
                _radioOverlayWindow.ShowInTaskbar = !_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide);
         
        }

        private void IL2Refocus_OnClick_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RefocusIL2, (bool) RefocusIL2.IsChecked);
        }

        private void ExpandInputDevices_OnClick_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "You must restart SRS for this setting to take effect.\n\nTurning this on will allow almost any DirectX device to be used as input expect a Mouse but may cause issues with other devices being detected",
                "Restart SimpleRadio Standalone", MessageBoxButton.OK,
                MessageBoxImage.Warning);

            _globalSettings.SetClientSetting(GlobalSettingsKeys.ExpandControls, (bool) ExpandInputDevices.IsChecked);
        }

        private void LaunchAddressTab(object sender, RoutedEventArgs e)
        {
            TabControl.SelectedItem = FavouritesSeversTab;
        }

        private void MicAGC_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AGC, (bool) MicAGC.IsChecked);
        }

        private void MicDenoise_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.Denoise, (bool) MicDenoise.IsChecked);
        }

        private void RadioSoundEffects_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioEffects,
                (bool) RadioSoundEffects.IsChecked);
        }

        private void RadioTxStart_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioTxEffects_Start,(bool) RadioTxStartToggle.IsChecked);
        }

        private void RadioTxEnd_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioTxEffects_End,(bool) RadioTxEndToggle.IsChecked);
        }

        private void RadioRxStart_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioRxEffects_Start,(bool) RadioRxStartToggle.IsChecked);
        }

        private void RadioRxEnd_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioRxEffects_End, (bool) RadioRxEndToggle.IsChecked);
        }

        private void RadioMIDS_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.MIDSRadioEffect, (bool)RadioMIDSToggle.IsChecked);
        }

        private void AudioSelectChannel_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.AutoSelectPresetChannel, (bool) AutoSelectChannel.IsChecked);
        }

        private void RadioSoundEffectsClipping_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioEffectsClipping,
                (bool) RadioSoundEffectsClipping.IsChecked);

        }

        private void MinimiseToTray_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.MinimiseToTray, (bool) MinimiseToTray.IsChecked);
        }

        private void StartMinimised_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.StartMinimised,(bool)StartMinimised.IsChecked);
        }

        private void AllowIL2PTT_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.AllowIL2PTT,(bool)AllowIL2PTT.IsChecked);
        }

        private void AlwaysAllowHotas_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.AlwaysAllowHotasControls,(bool)AlwaysAllowHotas.IsChecked);
        }

        private void CheckForBetaUpdates_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.CheckForBetaUpdates,(bool)CheckForBetaUpdates.IsChecked);
        }

        private void PlayConnectionSounds_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.PlayConnectionSounds, (bool)PlayConnectionSounds.IsChecked);
        }

    
        private void RescanInputDevices(object sender, RoutedEventArgs e)
        {
            InputManager.InitDevices();
            MessageBox.Show(this,
                "Input Devices Rescanned",
                "New input devices can now be used.",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void SetSRSPath_Click(object sender, RoutedEventArgs e)
        {
            Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\IL2-SR-Standalone","SRPathStandalone",Directory.GetCurrentDirectory());

            MessageBox.Show(this,
                "SRS Path set to: " + Directory.GetCurrentDirectory(),
                "SRS Client Path",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RequireAdminToggle_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RequireAdmin, (bool)RequireAdminToggle.IsChecked);
            MessageBox.Show(this,
                "SRS Requires admin rights to be able to read keyboard input in the background. \n\nIf you do not use any keyboard binds you can disable SRS Admin Privileges. \n\nFor this setting to take effect SRS must be restarted",
                "SRS Admin Privileges", MessageBoxButton.OK, MessageBoxImage.Warning);

        }

        private void CreateProfile(object sender, RoutedEventArgs e)
        {
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
                {
                    if (name.Trim().Length > 0)
                    {
                        _globalSettings.ProfileSettingsStore.AddNewProfile(name);
                        InitSettingsProfiles();
                     
                    }
                });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = this;
            inputProfileWindow.ShowDialog();
        }

        private void DeleteProfile(object sender, RoutedEventArgs e)
        {
            var current = ControlsProfile.SelectedValue as string;

            if (current.Equals("default"))
            {
                MessageBox.Show(this,
                    "Cannot delete the default input!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var result = MessageBox.Show(this,
                    $"Are you sure you want to delete {current} ?",
                    "Confirmation",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    ControlsProfile.SelectedIndex = 0;
                    _globalSettings.ProfileSettingsStore.RemoveProfile(current);
                    InitSettingsProfiles();
                }

            }

        }

        private void RenameProfile(object sender, RoutedEventArgs e)
        {

            var current = ControlsProfile.SelectedValue as string;
            if (current.Equals("default"))
            {
                MessageBox.Show(this,
                    "Cannot rename the default input!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var oldName = current;
                var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
                {
                    if (name.Trim().Length > 0)
                    {
                        _globalSettings.ProfileSettingsStore.RenameProfile(oldName,name);
                        InitSettingsProfiles();
                    }
                }, true,oldName);
                inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                inputProfileWindow.Owner = this;
                inputProfileWindow.ShowDialog();
            }

        }


        private void CopyProfile(object sender, RoutedEventArgs e)
        {
            var current = ControlsProfile.SelectedValue as string;
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
            {
                if (name.Trim().Length > 0)
                {
                    _globalSettings.ProfileSettingsStore.CopyProfile(current,name);
                    InitSettingsProfiles();
                }
            });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = this;
            inputProfileWindow.ShowDialog();
        }

        private void AlwaysAllowTransponderOverlay_OnClick(object sender, RoutedEventArgs e)
        {

            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.AlwaysAllowTransponderOverlay, (bool)AlwaysAllowTransponderOverlay.IsChecked);
        }


        private void ShowClientList_OnClick(object sender, RoutedEventArgs e)
        {
            if ((_clientListWindow == null) || !_clientListWindow.IsVisible ||
                (_clientListWindow.WindowState == WindowState.Minimized))
            {
                _clientListWindow?.Close();

                _clientListWindow = new ClientListWindow();
                _clientListWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _clientListWindow.Owner = this;
                _clientListWindow.Show();
            }
            else
            {
                _clientListWindow?.Close();
                _clientListWindow = null;
            }
        }

        private void ShowTransmitterName_OnClick_OnClick(object sender, RoutedEventArgs e)
        { 
            _globalSettings.SetClientSetting(GlobalSettingsKeys.ShowTransmitterName, ((bool)ShowTransmitterName.IsChecked).ToString());
        }
    }
}