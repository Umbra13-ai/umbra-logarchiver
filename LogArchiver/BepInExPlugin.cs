namespace LogArchiver
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using BepInEx;
    using UnityEngine;

    /// <summary>
    /// Keeps a copy of every session's logs, which the game and BepInEx otherwise overwrite at each launch.
    /// </summary>
    /// <remarks>
    /// Each session gets its own dated folder. The logs are copied there during play and when the game closes.
    /// Nothing in the archive is ever deleted. The plugin patches nothing in the game.
    /// </remarks>
    [BepInPlugin(ModId, ModName, ModVersion)]
    internal class BepInExPlugin : BaseUnityPlugin
    {
        internal const string ModId = "com.demeorevolutions.logarchiver";
        internal const string ModName = "LogArchiver";
        internal const string ModVersion = "3.12.0";

        private const string FolderTimeFormat = "yyyy-MM-dd_HH-mm-ss";

        private readonly object _copyLock = new object();
        private string _sessionFolder = string.Empty;
        private string _bepInExLog = string.Empty;
        private string _unityLog = string.Empty;
        private string _gameDataFolder = string.Empty;
        private Timer? _timer;
        private bool _hasLoggedCopyError;

        private void Awake()
        {
            try
            {
                Setup();
            }
            catch (Exception e)
            {
                Logger.LogError($"Log archiving is off for this session: {e}");
            }
        }

        private void OnApplicationQuit()
        {
            _timer?.Dispose();
            _timer = null;
            CopySessionLogs();
        }

        private void Setup()
        {
            var enabled = Config.Bind(
                "General",
                "Enabled",
                true,
                "Copy each session's logs into the archive folder. Nothing in the archive is ever deleted.");
            var archiveFolder = Config.Bind(
                "General",
                "ArchiveFolder",
                string.Empty,
                "Folder for the archived logs. Leave empty to use a LogArchive folder next to Player.log.");
            var intervalMinutes = Config.Bind(
                "General",
                "CopyIntervalMinutes",
                2,
                "How often logs are copied during play, so a crash loses at most this many minutes of log.");

            if (!enabled.Value)
            {
                return;
            }

            _gameDataFolder = Application.persistentDataPath;
            _unityLog = string.IsNullOrEmpty(Application.consoleLogPath)
                ? Path.Combine(_gameDataFolder, "Player.log")
                : Application.consoleLogPath;
            _bepInExLog = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");

            var archiveRoot = string.IsNullOrWhiteSpace(archiveFolder.Value)
                ? Path.Combine(_gameDataFolder, "LogArchive")
                : archiveFolder.Value;
            Directory.CreateDirectory(archiveRoot);

            // Unity renames the last session's Player.log to Player-prev.log at launch, so it holds that
            // session's complete log, including anything written after the last periodic copy.
            var unityLogFolder = Path.GetDirectoryName(_unityLog) ?? _gameDataFolder;
            ArchivePreviousUnityLog(archiveRoot, Path.Combine(unityLogFolder, "Player-prev.log"));

            _sessionFolder = Path.Combine(archiveRoot, DateTime.Now.ToString(FolderTimeFormat, CultureInfo.InvariantCulture));
            Directory.CreateDirectory(_sessionFolder);
            File.WriteAllText(Path.Combine(_sessionFolder, "session.txt"), DescribeSession(), Encoding.UTF8);

            var interval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes.Value));
            _timer = new Timer(_ => CopySessionLogs(), null, interval, interval);
            Logger.LogInfo($"Archiving this session's logs to {_sessionFolder}");
        }

        private string DescribeSession()
        {
            return new StringBuilder()
                .AppendLine($"Session started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .AppendLine($"Game version: {Application.version}")
                .AppendLine($"Unity version: {Application.unityVersion}")
                .AppendLine($"Mod log: {_bepInExLog}")
                .AppendLine($"Unity log: {_unityLog}")
                .ToString();
        }

        /// <summary>
        /// Stores the previous session's complete Unity log in that session's archive folder.
        /// </summary>
        private void ArchivePreviousUnityLog(string archiveRoot, string previousLog)
        {
            try
            {
                if (!File.Exists(previousLog))
                {
                    return;
                }

                var previousLogTime = File.GetLastWriteTime(previousLog);
                var latestSession = Directory.GetDirectories(archiveRoot)
                    .Select(dir => (Dir: dir, Started: ParseFolderTime(Path.GetFileName(dir))))
                    .Where(entry => entry.Started.HasValue)
                    .OrderByDescending(entry => entry.Started)
                    .FirstOrDefault();

                if (latestSession.Started.HasValue && latestSession.Started.Value <= previousLogTime)
                {
                    // The previous session was archived; replace its last periodic copy with the complete log.
                    CopyFile(previousLog, Path.Combine(latestSession.Dir, "Player.log"));
                    return;
                }

                // The previous session ran without this plugin. Keep its Unity log in a folder of its own.
                var folder = Path.Combine(
                    archiveRoot,
                    previousLogTime.ToString(FolderTimeFormat, CultureInfo.InvariantCulture) + "_unity-log-only");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    CopyFile(previousLog, Path.Combine(folder, "Player.log"));
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Could not archive the previous session's Unity log: {e.Message}");
            }
        }

        private static DateTime? ParseFolderTime(string folderName)
        {
            var timePart = folderName.Length >= FolderTimeFormat.Length
                ? folderName.Substring(0, FolderTimeFormat.Length)
                : folderName;
            return DateTime.TryParseExact(
                timePart,
                FolderTimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time)
                ? time
                : (DateTime?)null;
        }

        private void CopySessionLogs()
        {
            if (string.IsNullOrEmpty(_sessionFolder))
            {
                return;
            }

            lock (_copyLock)
            {
                TryCopy(_bepInExLog);
                TryCopy(_unityLog);
                foreach (var gameLog in Directory.GetFiles(_gameDataFolder, "MotherBrainLog*.txt"))
                {
                    TryCopy(gameLog);
                }
            }
        }

        private void TryCopy(string source)
        {
            try
            {
                if (File.Exists(source))
                {
                    CopyFile(source, Path.Combine(_sessionFolder, Path.GetFileName(source)));
                }
            }
            catch (Exception e)
            {
                // Log once per session so a locked file cannot flood the log every interval.
                if (!_hasLoggedCopyError)
                {
                    _hasLoggedCopyError = true;
                    Logger.LogWarning($"Could not copy {source} to the log archive: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Copies a file that the game or BepInEx may still have open for writing.
        /// </summary>
        private static void CopyFile(string source, string destination)
        {
            using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.Read);
            input.CopyTo(output);
        }
    }
}
