using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NLog;
using System.Threading;

/*
    Monitor an IL2 dserver logs directory. In the server startup.cfg, set
      mission_text_log = 1
      text_log_folder = "logs\text\"
    to save text logs. Then point this code at the output directory.

    Every time a new mission starts, a new 'zero' log is emitted. We reset
    our knowledge of players coalitions at that point. When a player spawns
    a plane, we remember their coalition.

    We don't track players quitting or being killed, thus they never go back
    to the Spectator coalition. This might be ok - being a spectator ought
    to be benign.
*/

public class Il2LogMonitor
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private enum Coalition
    {
        Spectator,
        Allies,
        Axis,
    }

    private readonly string directory;
    private readonly Dictionary<string, Coalition> playerCoalitions;
    private bool keepRunning = true;

    public Il2LogMonitor(string directory)
    {
        this.directory = directory;
        this.playerCoalitions = new Dictionary<string, Coalition>();
    }

    // TODO: Correct way to spin up a thread and stop later?
    public void Start()
    {
        Logger.Info("Catching up on latest log files...");
        // Find the newest 'zero' log, and process that and any more 
        // recent log files. This brings us up to date with a currently
        // running mission (if any).
        foreach (string logfile in GetNewestZeroLogAndSubsequentFiles())
        {
            ProcessLog(logfile);
        }

        // Now use the filesystem to watch for new logs being created.
        Logger.Info("Monitoring for new log files...");
        using (FileSystemWatcher watcher = new FileSystemWatcher())
        {
            watcher.Path = directory;
            watcher.Filter = "missionReport*.txt";
            watcher.Created += OnNewLogFile;

            watcher.EnableRaisingEvents = true;

            while(keepRunning)
            {
                Thread.Sleep(500);
            }
        }
    }

    private void OnNewLogFile(object source, FileSystemEventArgs e) =>
        ProcessLog(e.FullPath);

    private List<string> GetNewestZeroLogAndSubsequentFiles()
    {
        string[] allLogs = Directory.GetFiles(directory, "missionReport*.txt");
        Array.Sort(allLogs, new LogComparer());
        int mostRecentZeroLog = Int32.MaxValue;
        for (int i = allLogs.Length - 1; i >= 0; i--)
        {
            if (allLogs[i].Contains("[0].txt"))
            {
                mostRecentZeroLog = i;
                Console.WriteLine("Most recent zero log: " + allLogs[i]);
                break;
            }
        }
        List<string> result = new List<string>();
        for (int i = mostRecentZeroLog; i < allLogs.Length; i++)
        {
            result.Add(allLogs[i]);
        }
        return result;
    }

    private void ProcessLog(string logfile)
    {
        Console.WriteLine("Processing " + logfile);

        if (logfile.Contains("[0].txt"))
        {
            // 'Zero' log, so the map has rolled. Everything we know about coalitions is defunct.
            playerCoalitions.Clear();
        }
        using (StreamReader sr = new StreamReader(logfile))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                ProcessLogLine(line);
            }
            sr.Close();
        }
    }

    private readonly Regex RE_SPAWN = new Regex(
        @"^T:(?<tik>\d+) AType:10 PLID:(?<aircraft_id>\d+) PID:(?<bot_id>\d+) BUL:(?<cartridges>\d+) SH:(?<shells>\d+) BOMB:(?<bombs>\d+) RCT:(?<rockets>\d+) \((?<pos>.+)\) IDS:(?<profile_id>[-\w]{36}) LOGIN:(?<account_id>[-\w]{36}) NAME:(?<name>.*) TYPE:(?<aircraft_name>[\w\(\) .\-_/]+) COUNTRY:(?<country_id>\d{1,3}) FORM:(?<form>\d+) FIELD:(?<airfield_id>\d+) INAIR:(?<airstart>\d) PARENT:(?<parent_id>[-\d]+) ISPL:(?<is_player>\d+) ISTSTART:(?<is_tracking_stat>\d+) PAYLOAD:(?<payload_id>\d+) FUEL:(?<fuel>\S{5,6}) SKIN:(?<skin>[\S ]*) WM:(?<weapon_mods_id>\d+)",
        RegexOptions.Compiled);

    private void ProcessLogLine(string line)
    {
        Match m = RE_SPAWN.Match(line);
        if (m.Success)
        {
            string name = m.Groups["name"].Value;
            string country_id = m.Groups["country_id"].Value;
            Coalition coalition = CoalitionFor(country_id);
            playerCoalitions[name] = coalition;
            Console.WriteLine("Set player {0} to coalition {1} ", name, coalition);
        }
    }

    private Coalition CoalitionFor(string country_id)
    {
        if (country_id.StartsWith("1"))
        {
            return Coalition.Allies;
        }
        else if (country_id.StartsWith("2"))
        {
            return Coalition.Axis;
        }
        else
        {
            return Coalition.Spectator;
        }
    }

    class LogComparer : IComparer<string>
    {
        // Log file names look like this: missionReport(2020-08-05_03-42-16)[52].txt
        // Can't compare them alphabetically. Need to pull out the date/time component
        // and the file sequence number and compare on those two.
        private readonly Regex rx = new Regex(@"missionReport\((.+)\)\[(\d+)\]\.txt$", RegexOptions.Compiled);

        public int Compare(string x, string y)
        {
            (string x_time, int x_num) = GetLogTimeAndNumberFromFilename(x);
            (string y_time, int y_num) = GetLogTimeAndNumberFromFilename(y);

            int compareByTime = x_time.CompareTo(y_time);
            if (compareByTime != 0)
            {
                return compareByTime;
            }
            return x_num.CompareTo(y_num);
        }

        private (string, int) GetLogTimeAndNumberFromFilename(string fileName)
        {
            Match match = rx.Match(fileName);
            if (match.Success)
            {
                string logTime = match.Groups[1].Value;
                int logNum = Int32.Parse(match.Groups[2].Value);
                return (logTime, logNum);
            }
            else
            {
                // TODO: Gross.
                throw new Exception("Could not get log time and number from: " + fileName);
            }
        }
    }
}