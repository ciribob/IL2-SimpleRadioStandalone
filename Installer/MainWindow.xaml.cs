using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using IWshRuntimeLibrary;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using File = System.IO.File;
using ThreadState = System.Diagnostics.ThreadState;

namespace Installer
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private const string REG_PATH = "HKEY_CURRENT_USER\\SOFTWARE\\IL2-SRS";
        private readonly string _currentDirectory;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private ProgressBarDialog _progressBarDialog = null;

        public MainWindow()
        {
            SetupLogging();
            InitializeComponent();

            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            var version = fvi.FileVersion;

            intro.Content = intro.Content + " v" + version;

            //allows click and drag anywhere on the window
            containerPanel.MouseLeftButtonDown += GridPanel_MouseLeftButtonDown;

            var srPathStr = ReadPath("SRSPath");
            if (srPathStr != "")
            {
                srPath.Text = srPathStr;
            }

            var scriptsPath = ReadPath("IL2Path");
            if (scriptsPath != "")
            {
                IL2ScriptsPath.Text = scriptsPath;
            }
            else
            {
                IL2ScriptsPath.Text = "";
            }

            //To get the location the assembly normally resides on disk or the install directory
            var currentPath = GetWorkingDirectory();

            if (currentPath.StartsWith("file:\\"))
            {
                currentPath = currentPath.Replace("file:\\", "");
            }

            _currentDirectory = currentPath;

            Logger.Info("Listing Files / Directories for: "+_currentDirectory);
            ListFiles(_currentDirectory);
            Logger.Info("Finished Listing Files / Directories");

            if (!CheckExtracted())
            {

                MessageBox.Show(
                    "Please extract the entire installation zip into a folder and then run the installer from the extracted folder.\n\nDo not run the installer from the zip as it wont work!",
                    "Please Extract Installation zip",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                Logger.Warn("IL2 is Running - Installer quit");

                Environment.Exit(0);

                return;
            }

            var hyperlinks = WPFElementHelper.GetVisuals(HelpText).OfType<Hyperlink>();
            foreach (var link in hyperlinks)
                link.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler((sender, args) =>
                {
                    Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri));
                    args.Handled = true;
                });

        }

        private bool CheckExtracted()
        {
            return File.Exists(_currentDirectory + "\\opus.dll")
                   && File.Exists(_currentDirectory + "\\speexdsp.dll")
                   && File.Exists(_currentDirectory + "\\IL2-SR-ClientRadio.exe")
                   && File.Exists(_currentDirectory + "\\IL2-SR-Server.exe");
        }


        private void SetupLogging()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string logFilePath = Path.Combine(baseDirectory, "installer-log.txt");
            string oldLogFilePath = Path.Combine(baseDirectory, "install-log.old.txt");

            FileInfo logFileInfo = new FileInfo(logFilePath);
            // Cleanup logfile if > 100MB, keep one old file copy
            if (logFileInfo.Exists && logFileInfo.Length >= 104857600)
            {
                if (File.Exists(oldLogFilePath))
                {
                    try
                    {
                        File.Delete(oldLogFilePath);
                    }
                    catch (Exception) { }
                }

                try
                {
                    File.Move(logFilePath, oldLogFilePath);
                }
                catch (Exception) { }
            }

            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget();

            fileTarget.FileName = "${basedir}/installer-log.txt";
            fileTarget.Layout =
                @"${longdate} | ${logger} | ${message} ${exception:format=toString,Data:maxInnerExceptionLevel=2}";

            var wrapper = new AsyncTargetWrapper(fileTarget, 5000, AsyncTargetWrapperOverflowAction.Discard);
            config.AddTarget("file", wrapper);

#if DEBUG
            config.LoggingRules.Add( new LoggingRule("*", LogLevel.Debug, fileTarget));
#else
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Info, fileTarget));
#endif

            LogManager.Configuration = config;

        }

        private async void  InstallReleaseButton(object sender, RoutedEventArgs e)
        {
            var IL2criptsPath = IL2ScriptsPath.Text;
            if ((bool)!InstallScriptsCheckbox.IsChecked)
            {
                IL2criptsPath = null;
            }
            else
            {
                var path = FindValidIL2Folder(IL2criptsPath);

                if (path.Length == 0)
                {
                    MessageBox.Show(
                           "Unable to find IL2 Game - Please check the path to the game directory",
                           "IL2-SRS Installer",
                           MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
            }

            InstallButton.IsEnabled = false;
            RemoveButton.IsEnabled = false;

            InstallButton.Content = "Installing...";

            _progressBarDialog = new ProgressBarDialog();
            _progressBarDialog.Owner = this;
            _progressBarDialog.Show();

            var srsPath = srPath.Text;
           
            var shortcut = CreateStartMenuShortcut.IsChecked ?? true;

            new Action(async () =>
            {
                int result = await Task.Run<int>(() => InstallRelease(srsPath,IL2criptsPath, shortcut));
                if (result == 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        {
                            InstallButton.IsEnabled = true;
                            RemoveButton.IsEnabled = true;
                            InstallButton.Content = "Install";
                        }
                    ); //end-invoke
                    _progressBarDialog.UpdateProgress(true, "Error");

                }
                else if (result == 1)
                {
                    _progressBarDialog.UpdateProgress(true, "Installed IL2-SRS Successfully!");

                    Logger.Info($"Installed IL2-SRS Successfully!");
                
                    //open to installation location
                    Process.Start("explorer.exe", srPath.Text);
                    Environment.Exit(0);
                }
                else
                {
                    _progressBarDialog.UpdateProgress(true, "Error with Installation");

                    MessageBox.Show(
                        "Error with installation - please post your installer-log.txt on the SRS Discord for Support under IL2-SRS support",
                        "Installation Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                
                    Process.Start("https://discord.gg/vqxAw7H");
                    Process.Start("explorer.exe", GetWorkingDirectory());
                    Environment.Exit(0);
                }
            }).Invoke();

        }

        private int InstallRelease(string srPath, string IL2ScriptsPath, bool shortcut)
        {
            try
            {
                QuitSimpleRadio();

                var path = "";
                if (IL2ScriptsPath != null)
                {
                    path = FindValidIL2Folder(IL2ScriptsPath);

                    if (path.Length == 0)
                    {

                        MessageBox.Show(
                            "Unable to find IL2 Game - Please check the path to the game directory",
                            "IL2-SRS Installer",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return 0;
                    }


                    Logger.Info($"Installing - Paths: \nProgram:{srPath} \nIL2:{IL2ScriptsPath} ");

                    RemoveIL2Install(srPath, IL2ScriptsPath);

                    EnableTelemetry(path);
                }
                else
                {
                    Logger.Info($"Installing - Paths: \nProgram:{srPath} IL2: NO PATH - NO CONFIG");
                }

                //install program
                InstallProgram(srPath);

                WritePath(srPath, "SRSPath");

                if(IL2ScriptsPath!=null)
                    WritePath(IL2ScriptsPath, "IL2Path");

                if (shortcut)
                {
                    InstallShortcuts(srPath);
                }


                if (IL2ScriptsPath != null)
                {
                    string message = "Installation / Update Completed Successfully!\nConfigured IL2 at: \n";

                    message += ("\n" + path);
                    
                    MessageBox.Show(message, "IL2-SRS Installer",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string message = "Installation / Update Completed Successfully!";

                    MessageBox.Show(message, "IL2-SRS Installer",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                return 1;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error Running IL2-SRS Installer");

                return -1;
            }
        }

        private string GetWorkingDirectory()
        {
            return new FileInfo(Assembly.GetEntryAssembly().Location).Directory.ToString();
        }

        static void ListFiles(string sDir)
        {
            try
            {
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        Logger.Info(f);
                    }
                    ListFiles(d);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex,"Error listing files");
            }
        }


        private void RemoveIL2Install(string programPath, string IL2Path)
        {
            Logger.Info($"Removed IL2-SRS Version at {programPath} and {IL2Path}");

            var path = FindValidIL2Folder(IL2Path);

            _progressBarDialog.UpdateProgress(false, $"Removing IL2-SRS at {path}");

            Logger.Info($"Removed IL2-SRS program files at {programPath}");
            _progressBarDialog.UpdateProgress(false, $"Removing IL2-SRS at {programPath}");
            if (Directory.Exists(programPath) && File.Exists(programPath + "\\IL2-SR-ClientRadio.exe"))
            {
                DeleteFileIfExists(programPath + "\\IL2-SR-ClientRadio.exe");
                DeleteFileIfExists(programPath + "\\IL2-SRS-External-Audio.exe");
                DeleteFileIfExists(programPath + "\\opus.dll");
                DeleteFileIfExists(programPath + "\\speexdsp.dll");
                DeleteFileIfExists(programPath + "\\SRS-AutoUpdater.exe");
                DeleteFileIfExists(programPath + "\\IL2-SR-ClientRadio.exe");
                DeleteFileIfExists(programPath + "\\serverlog.txt");
                DeleteFileIfExists(programPath + "\\clientlog.txt");
              //  DeleteFileIfExists(programPath + "\\default.cfg");
              //  DeleteFileIfExists(programPath + "\\global.cfg");

                DeleteDirectory(programPath + "\\AudioEffects");
            }
            Logger.Info($"Finished clearing config and program ");
        }

        private static string ReadPath(string key)
        {
            var srPath = (string) Registry.GetValue(REG_PATH,
                key,
                "");

            return srPath ?? "";
        }

        private static void WritePath(string path, string key)
        {
            Registry.SetValue(REG_PATH,
                key,
                path);
        }


        private static void DeleteRegKeys()
        {
            try
            {
                Registry.SetValue(REG_PATH,
                    "SRSPath",
                    "");
                Registry.SetValue(REG_PATH,
                    "IL2Path",
                    "");
            }
            catch (Exception ex)
            {
            }

            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey("SOFTWARE", true))
                {
                    key.DeleteSubKeyTree("IL2-SRS", false);
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void QuitSimpleRadio()
        {
            Logger.Info($"Closing IL2-SRS Client & Server");
#if DEBUG
            return;
#endif
            foreach (var clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.ToLower().Trim().StartsWith("il2-sr-") )
                {
                    Logger.Info($"Found & Terminating {clsProcess.ProcessName}");
                    clsProcess.Kill();
                    clsProcess.WaitForExit(5000);
                    clsProcess.Dispose();
                }
            }
            Logger.Info($"Closed SRS Client & Server");
        }

        private void GridPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }


        private void Set_Install_Path(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderBrowserDialog();
            var result = dlg.ShowDialog();
            if (result.ToString() == "OK")
            {
                // Open document
                var filename = dlg.SelectedPath;

                if (!filename.EndsWith("\\"))
                {
                    filename = filename + "\\";
                }
                filename = filename + "IL2-SRS\\";

                srPath.Text = filename;
            }
        }

        private void Set_Scripts_Path(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderBrowserDialog();
            var result = dlg.ShowDialog();
            if (result.ToString() == "OK")
            {
                // Open document
                var filename = dlg.SelectedPath;

                if (!filename.EndsWith("\\"))
                {
                    filename = filename + "\\";
                }

                IL2ScriptsPath.Text = filename;
            }
        }


        private static string FindValidIL2Folder(string path)
        {
            Logger.Info($"Finding IL2 Game Path");
        
            if(path == null || path.Length == 0)
            {
                return "";
            }

            //need bin & data folder at the root
            if (Directory.Exists(path + "\\bin") && Directory.Exists(path + "\\data") &&
                File.Exists(path + "\\data\\startup.cfg"))
            {
                Logger.Info($"Fould IL2 startup.cfg "+path);
                return path;
            }

            Logger.Info($"Could not find IL2 startup.cfg " + path);
            return "";
        }

        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                }
            }
        }

        private void InstallProgram(string path)
        {
            Logger.Info($"Installing IL2-SRS Program to {path}");
            _progressBarDialog.UpdateProgress(false, $"Installing SRS at {path}");
            //sleep! WTF directory is lagging behind state here...
            Task.Delay(TimeSpan.FromMilliseconds(200)).Wait();

            _progressBarDialog.UpdateProgress(false, $"Creating Directories at {path}");

            Logger.Info($"Creating Directories");
            CreateDirectory(path);
            CreateDirectory(path + "\\AudioEffects");

            //sleep! WTF directory is lagging behind state here...
            Task.Delay(TimeSpan.FromMilliseconds(200)).Wait();
            _progressBarDialog.UpdateProgress(false, $"Copying Program Files at {path}");

            Logger.Info($"Copying binaries");
            File.Copy(_currentDirectory + "\\opus.dll", path + "\\opus.dll", true);
            File.Copy(_currentDirectory + "\\speexdsp.dll", path + "\\speexdsp.dll", true);
        //    File.Copy(_currentDirectory + "\\Readme.txt", path + "\\Readme.txt", true);
            File.Copy(_currentDirectory + "\\IL2-SR-ClientRadio.exe", path + "\\IL2-SR-ClientRadio.exe", true);
            File.Copy(_currentDirectory + "\\IL2-SR-Server.exe", path + "\\IL2-SR-Server.exe", true);
            File.Copy(_currentDirectory + "\\IL2-SRS-AutoUpdater.exe", path + "\\IL2-SRS-AutoUpdater.exe", true);
            File.Copy(_currentDirectory + "\\IL2-SRS-External-Audio.exe", path + "\\IL2-SRS-External-Audio.exe", true);

            Logger.Info($"Copying directories");
            DirectoryCopy(_currentDirectory+"\\AudioEffects", path+"\\AudioEffects");

            Logger.Info($"Finished installing IL2-SRS Program to {path}");

        }

        private void InstallShortcuts(string path)
        {
            Logger.Info($"Adding IL2-SRS Shortcut");
            string executablePath = Path.Combine(path, "IL2-SR-ClientRadio.exe");
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "IL2-SRS Client.lnk");

            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);

            shortcut.Description = "IL2-SRS Client";
            shortcut.TargetPath = executablePath;
            shortcut.WorkingDirectory = path;
            shortcut.Save();
        }

        private bool IsReadOnly(string path)
        {
            var file = new FileInfo(path);

            return file.IsReadOnly;
        }

        private void SetReadOnly(string path,bool readOnly)
        {
            var file = new FileInfo(path);

            if (readOnly)
            {
                Logger.Info($"Config present at {path} set to Read Only");
                File.SetAttributes(path, FileAttributes.ReadOnly);
            }
            else
            {
                Logger.Info($"Config present at {path} set to Writable");
                File.SetAttributes(path, ~FileAttributes.ReadOnly);
            }
        }

        private void EnableTelemetry(string path)
        {
            var cfgPath = path + "\\data\\startup.cfg";
            Logger.Info($"Installing Config to {cfgPath}");

            //check if its read only
            bool readOnly = IsReadOnly(cfgPath);

            if (readOnly)
            {
                Logger.Info($"Config present at {path} is Read Only");
                //temporarily make readable
                SetReadOnly(cfgPath, false);
            }

            _progressBarDialog.UpdateProgress(false, $"Enable SRS Telemetry @ {cfgPath}");
            
            var lines = File.ReadAllText(cfgPath);

            if (lines.Contains("telemetrydevice"))
            {
                //handle existing file
                if (lines.Contains("127.0.0.1:4322") 
                    || ( lines.Contains("\"127.0.0.1\"") && lines.Contains("4322")) 
                    || lines.Contains("addr1"))
                {
                    //already there
                    Logger.Info($"Config present at {path}");
                }
                else
                {
                    //extract telemetry
                    var allLines = File.ReadAllLines(cfgPath);
                    
                    for (int i=0;i<allLines.Length;i++)
                    {
                        if (allLines[i].Contains("addr") && !allLines[i].Contains("addr1"))
                        {
                            allLines[i] = allLines[i] + "\r\n\taddr1 = \"127.0.0.1:4322\"";

                            Logger.Info($"Appending addr1 - likely JetSeat in use {cfgPath}");
                        }
                    }

                    File.WriteAllLines(cfgPath, allLines);
                }
            }
            else
            {
                var telemetry =
                    "[KEY = telemetrydevice]\r\n\taddr = \"127.0.0.1\"\r\n\tdecimation = 2\r\n\tenable = true\r\n\tport = 4322\r\n[END]";
                File.AppendAllText(cfgPath, telemetry);

                Logger.Info($"No Telemtry - Appending to config {cfgPath}");
            }

            Logger.Info($"Config installed to {cfgPath}");

            if (readOnly)
            {
                SetReadOnly(cfgPath,true);
            }


            _progressBarDialog.UpdateProgress(false, $"Installed IL2-SRS Config @ {cfgPath}");
        }

        public static void DeleteDirectory(string target_dir)
        {
            if (Directory.Exists(target_dir))
            {
                Directory.Delete(target_dir, true);
            }
        }

        private void DirectoryCopy(string sourceDirName, string destDirName)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, true);
            }

            // If copying subdirectories, copy them and their contents to new location.
            foreach (DirectoryInfo subdir in dirs)
            {
                string temppath = Path.Combine(destDirName, subdir.Name);
                DirectoryCopy(subdir.FullName, temppath);
            }
            
        }

        private async Task<bool> UninstallSR(string srPath, string IL2ScriptsPath)
        {
            try
            {
                QuitSimpleRadio();
                Application.Current.Dispatcher.Invoke(() =>
                    {
                        InstallButton.IsEnabled = false;
                        RemoveButton.IsEnabled = false;

                        RemoveButton.Content = "Removing...";
                    }
                ); //end-invoke

                _progressBarDialog.UpdateProgress(false, $"Removing IL2-SRS");
                Logger.Info($"Removing - Paths: \nProgram:{srPath} \nIL2:{IL2ScriptsPath} ");
                RemoveIL2Install(srPath, IL2ScriptsPath);


                DeleteRegKeys();

                RemoveShortcuts();

                return true;

            }
            catch (Exception ex) 
            {
                Logger.Error(ex, "Error Running Uninstaller");
            }

            return false;

        }

        private void RemoveShortcuts()
        {
            Logger.Info($"Removed IL2-SRS Shortcut");
            string shortcutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "IL2-SRS Client.lnk");

            DeleteFileIfExists(shortcutPath);
        }

        private void CreateDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                var sid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);

                // Create the rules
                var writerule = new FileSystemAccessRule(sid, FileSystemRights.Write, AccessControlType.Allow);

                var dir = Directory.CreateDirectory(path);

                dir.Refresh();
                //sleep! WTF directory is lagging behind state here...
                Task.Delay(TimeSpan.FromMilliseconds(200)).Wait();

                var dSecurity = dir.GetAccessControl();
                dSecurity.AddAccessRule(new FileSystemAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.FullControl,
                    InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                    PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                dir.SetAccessControl(dSecurity);
                dir.Refresh();
            }

            //sometimes it says directory created and its not!
            do 
            { 
                Task.Delay(TimeSpan.FromMilliseconds(50)).Wait();
            } while(!Directory.Exists(path));
            Task.Delay(TimeSpan.FromMilliseconds(100)).Wait();
        }


        private async void Remove_Plugin(object sender, RoutedEventArgs e)
        {

            if (!Directory.Exists(IL2ScriptsPath.Text))
            {
                IL2ScriptsPath.Text = "";
                Logger.Info($"IL2 path not valid - ignoring uninstall of config: {IL2ScriptsPath.Text}");
            }

            _progressBarDialog = new ProgressBarDialog();
            _progressBarDialog.Owner = this;
            _progressBarDialog.Show();
            _progressBarDialog.UpdateProgress(false, "Uninstalling IL2-SRS");

            var result = await UninstallSR(srPath.Text,IL2ScriptsPath.Text);
            if (result)
            {
                _progressBarDialog.UpdateProgress(true, "Removed IL2-SRS Successfully!");
                Logger.Info($"Removed IL2-SRS Successfully!");

                MessageBox.Show(
                    "IL2-SRS Removed Successfully!\n\nContaining folder left just in case you want favourites",
                    "IL2-SRS Installer",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _progressBarDialog.UpdateProgress(true, "Error with Uninstaller");
                MessageBox.Show(
                    "Error with uninstaller - please post your installer-log.txt on the SRS Discord for Support under IL2-SRS Support",
                    "Installation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                Process.Start("https://discord.gg/vqxAw7H");

                Process.Start("explorer.exe", GetWorkingDirectory());

            }
            Environment.Exit(0);
        }

        private void EnableSRSConfig_OnChecked(object sender, RoutedEventArgs e)
        {
            IL2ScriptsPath.IsEnabled = true;
        }

        private void EnableSRSConfig_OnUnchecked(object sender, RoutedEventArgs e)
        {
            IL2ScriptsPath.IsEnabled = false;

        }
    }
}
